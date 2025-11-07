class SmartRouteMap {
    constructor() {
        console.log("SmartRouteMap constructor called"); // DEBUG
        this.map = null;
        this.startMarker = null;
        this.endMarker = null;
        this.routeLayer = null;
        this.trafficLayers = [];
        this.startCoord = null;
        this.endCoord = null;
        this.apiBaseUrl = 'https://localhost:7091/api'; // Update with your API URL

        this.init();
    }

    init() {
        console.log("Initializing map..."); // DEBUG
        this.initMap();
        this.bindEvents();
        this.loadTrafficData();

        // Refresh traffic data every 2 minutes
        setInterval(() => this.loadTrafficData(), 120000);
    }

    initMap() {
        console.log("Setting up map..."); // DEBUG
        // Initialize map centered on Rajkot
        this.map = L.map('map').setView([22.3039, 70.8022], 13);

        // Add OpenStreetMap tiles
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '© OpenStreetMap contributors'
        }).addTo(this.map);

        // Add click event for selecting points
        this.map.on('click', (e) => this.onMapClick(e));
        console.log("Map initialized successfully"); // DEBUG
    }

    bindEvents() {
        console.log("Binding events..."); // DEBUG
        const calculateBtn = document.getElementById('calculateRoute');
        const clearBtn = document.getElementById('clearRoute');
        const reportBtn = document.getElementById('reportTraffic');

        if (calculateBtn) {
            calculateBtn.addEventListener('click', () => {
                console.log("Calculate route button clicked!"); // DEBUG
                this.calculateRoute();
            });
        } else {
            console.error("Calculate route button not found!");
        }

        if (clearBtn) {
            clearBtn.addEventListener('click', () => this.clearRoute());
        }

        if (reportBtn) {
            reportBtn.addEventListener('click', () => this.reportTraffic());
        }
    }

    onMapClick(e) {
        const { lat, lng } = e.latlng;
        console.log(`Map clicked at: ${lat}, ${lng}`); // DEBUG

        if (!this.startCoord) {
            this.setStartPoint(lat, lng);
        } else if (!this.endCoord) {
            this.setEndPoint(lat, lng);
        } else {
            // Reset and set new start point
            this.clearRoute();
            this.setStartPoint(lat, lng);
        }
    }

    setStartPoint(lat, lng) {
        console.log(`Setting start point: ${lat}, ${lng}`); // DEBUG
        this.startCoord = { lat, lng };

        if (this.startMarker) {
            this.map.removeLayer(this.startMarker);
        }

        this.startMarker = L.marker([lat, lng], {
            icon: L.icon({
                iconUrl: 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-green.png',
                shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/0.7.7/images/marker-shadow.png',
                iconSize: [25, 41],
                iconAnchor: [12, 41],
                popupAnchor: [1, -34],
                shadowSize: [41, 41]
            })
        }).addTo(this.map);

        this.startMarker.bindPopup('Start Point').openPopup();
        document.getElementById('startLocation').value = `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
    }

    setEndPoint(lat, lng) {
        console.log(`Setting end point: ${lat}, ${lng}`); // DEBUG
        this.endCoord = { lat, lng };

        if (this.endMarker) {
            this.map.removeLayer(this.endMarker);
        }

        this.endMarker = L.marker([lat, lng], {
            icon: L.icon({
                iconUrl: 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-red.png',
                shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/0.7.7/images/marker-shadow.png',
                iconSize: [25, 41],
                iconAnchor: [12, 41],
                popupAnchor: [1, -34],
                shadowSize: [41, 41]
            })
        }).addTo(this.map);

        this.endMarker.bindPopup('End Point').openPopup();
        document.getElementById('endLocation').value = `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
    }

    async calculateRoute() {
        console.log("calculateRoute called"); // DEBUG
        console.log("Start coord:", this.startCoord); // DEBUG
        console.log("End coord:", this.endCoord); // DEBUG

        if (!this.startCoord || !this.endCoord) {
            alert('Please select both start and end points on the map');
            return;
        }

        const avoidTraffic = document.getElementById('avoidTraffic').checked;
        console.log("Avoid traffic:", avoidTraffic); // DEBUG

        const url = `${this.apiBaseUrl}/Routes?startLat=${this.startCoord.lat}&startLng=${this.startCoord.lng}&endLat=${this.endCoord.lat}&endLng=${this.endCoord.lng}&avoidTraffic=${avoidTraffic}`;
        console.log("API URL:", url); // DEBUG

        try {
            console.log("Fetching route..."); // DEBUG
            const response = await fetch(url);
            console.log("Response status:", response.status); // DEBUG
            console.log("Response:", response); // DEBUG

            if (!response.ok) {
                const errorText = await response.text();
                console.error('API Error Response:', errorText);
                throw new Error(`Failed to calculate route: ${response.status} ${response.statusText}`);
            }

            const routeData = await response.json();
            console.log('Route data received:', routeData); // DEBUG
            this.displayRoute(routeData);
        } catch (error) {
            console.error('Error calculating route:', error);
            alert(`Failed to calculate route: ${error.message}`);
        }
    }

    displayRoute(routeData) {
        console.log("Displaying route:", routeData); // DEBUG

        // Clear existing route
        if (this.routeLayer) {
            this.map.removeLayer(this.routeLayer);
        }

        if (routeData.path && routeData.path.length > 0) {
            const pathCoords = routeData.path.map(point => [point.latitude, point.longitude]);
            console.log("Path coordinates:", pathCoords); // DEBUG

            // Create route line with different color based on traffic
            const routeColor = routeData.hasTrafficDetours ? '#ff6b35' : '#2563eb';

            this.routeLayer = L.polyline(pathCoords, {
                color: routeColor,
                weight: 5,
                opacity: 0.8
            }).addTo(this.map);

            // Fit map to show entire route
            this.map.fitBounds(this.routeLayer.getBounds(), { padding: [20, 20] });

            // Update route information
            this.updateRouteInfo(routeData);
        } else {
            console.error("No path data in route response"); // DEBUG
        }
    }

    updateRouteInfo(routeData) {
        const routeInfo = document.getElementById('routeInfo');
        const distance = routeData.totalDistance?.toFixed(2) || 'N/A';
        const time = Math.round(routeData.estimatedTime) || 'N/A';
        const trafficWarning = routeData.hasTrafficDetours ?
            '<div class="alert alert-warning alert-sm mt-2">⚠️ Route adjusted for traffic</div>' : '';

        routeInfo.innerHTML = `
            <div><strong>Distance:</strong> ${distance} km</div>
            <div><strong>Est. Time:</strong> ${time} minutes</div>
            <div><strong>Instructions:</strong></div>
            <ul class="list-unstyled mt-2">
                ${routeData.instructions?.map(instruction => `<li>• ${instruction}</li>`).join('') || '<li>No instructions available</li>'}
            </ul>
            ${trafficWarning}
        `;
    }

    clearRoute() {
        console.log("Clearing route"); // DEBUG

        // Remove markers
        if (this.startMarker) {
            this.map.removeLayer(this.startMarker);
            this.startMarker = null;
        }

        if (this.endMarker) {
            this.map.removeLayer(this.endMarker);
            this.endMarker = null;
        }

        // Remove route
        if (this.routeLayer) {
            this.map.removeLayer(this.routeLayer);
            this.routeLayer = null;
        }

        // Clear coordinates
        this.startCoord = null;
        this.endCoord = null;

        // Clear input fields
        document.getElementById('startLocation').value = '';
        document.getElementById('endLocation').value = '';
        document.getElementById('routeInfo').innerHTML = '<p>Select start and end points to calculate route</p>';
    }

    async loadTrafficData() {
        try {
            const response = await fetch(`${this.apiBaseUrl}/traffic/current`);
            if (response.ok) {
                const trafficData = await response.json();
                this.displayTrafficData(trafficData);
            }
        } catch (error) {
            console.error('Error loading traffic data:', error);
        }
    }

    displayTrafficData(trafficData) {
        // Clear existing traffic layers
        this.trafficLayers.forEach(layer => this.map.removeLayer(layer));
        this.trafficLayers = [];

        trafficData.forEach(traffic => {
            const color = this.getTrafficColor(traffic.trafficLevel);
            const marker = L.circleMarker([traffic.coordinates[1], traffic.coordinates[0]], {
                radius: 8,
                fillColor: color,
                color: color,
                weight: 2,
                opacity: 0.8,
                fillOpacity: 0.6
            }).addTo(this.map);

            marker.bindPopup(`
                <strong>Traffic Alert</strong><br>
                Road: ${traffic.roadName}<br>
                Level: ${this.getTrafficLevelText(traffic.trafficLevel)}<br>
                Reported: ${new Date(traffic.reportedAt).toLocaleTimeString()}
            `);

            this.trafficLayers.push(marker);
        });
    }

    getTrafficColor(level) {
        switch (level) {
            case 1: return '#fbbf24'; // Light - Yellow
            case 2: return '#f97316'; // Moderate - Orange
            case 3: return '#ef4444'; // Heavy - Red
            case 4: return '#7f1d1d'; // Blocked - Dark Red
            default: return '#6b7280'; // Unknown - Gray
        }
    }

    getTrafficLevelText(level) {
        switch (level) {
            case 1: return 'Light Traffic';
            case 2: return 'Moderate Traffic';
            case 3: return 'Heavy Traffic';
            case 4: return 'Road Blocked';
            default: return 'Unknown';
        }
    }

    async reportTraffic() {
        // Use map center as default location for traffic report
        const center = this.map.getCenter();
        const trafficLevel = parseInt(document.getElementById('trafficLevel').value);

        const trafficReport = {
            roadName: `Road at ${center.lat.toFixed(4)}, ${center.lng.toFixed(4)}`,
            trafficLevel: trafficLevel,
            latitude: center.lat,
            longitude: center.lng,
            reportedBy: 'Bus Driver' // You can make this dynamic
        };

        try {
            const response = await fetch(`${this.apiBaseUrl}/traffic/report`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(trafficReport)
            });

            if (response.ok) {
                alert('Traffic reported successfully!');
                this.loadTrafficData(); // Refresh traffic data
            } else {
                throw new Error('Failed to report traffic');
            }
        } catch (error) {
            console.error('Error reporting traffic:', error);
            alert('Failed to report traffic. Please try again.');
        }
    }
}

// Initialize the map when the page loads
document.addEventListener('DOMContentLoaded', () => {
    console.log("DOM Content Loaded - Initializing SmartRouteMap"); // DEBUG
    new SmartRouteMap();
});