# 🧭 Dynamic Pathfinding System – SmartRouteRajkot

The **Dynamic Pathfinding System** is a smart, map-based web API that determines the **shortest and most efficient route** between two points in **Rajkot City**.
It dynamically adapts to **real-time traffic updates**, providing **travel time estimation**, **traffic reporting**, and **route optimization** using **Dijkstra’s Algorithm** and **OpenStreetMap** data.

---

## 🚀 Features

* 🔹 **Shortest Route Calculation** — Implements **Dijkstra’s Algorithm** for optimal pathfinding.
* 🔹 **Dynamic Traffic Updates** — Adjusts route weights based on live traffic conditions.
* 🔹 **Estimated Travel Time (ETA)** — Calculates realistic ETA using the formula:
  `ETA = (Distance / 40 km/h) × Traffic Multiplier`.
* 🔹 **Real-Time Map Integration** — Displays routes interactively on the Rajkot map.
* 🔹 **Modular & Scalable Architecture** — Built with ASP.NET Core Web API and MongoDB.
* 🔹 **Swagger API Documentation** — Test and view endpoints easily in your browser.

---

## ⚙️ System Overview

| Component       | Description                        |
| --------------- | ---------------------------------- |
| **Backend**     | ASP.NET Core 8 Web API (C#)        |
| **Database**    | MongoDB                            |
| **Map Source**  | OpenStreetMap (via Overpass API)   |
| **Algorithm**   | Dijkstra’s Shortest Path Algorithm |
| **Language**    | C#                                 |
| **Data Format** | GeoJSON                            |

---

## 🧩 Algorithm – Dijkstra’s Shortest Path

The city’s road network is represented as a **weighted graph**, where:

* **Nodes** = intersections or coordinates
* **Edges** = road segments between nodes
* **Edge Weight** = distance × traffic multiplier

Traffic conditions are modeled as:

| Level    | Multiplier | Description                |
| -------- | ---------- | -------------------------- |
| Light    | 1.0        | Free flow                  |
| Moderate | 1.2        | Normal city traffic        |
| Heavy    | 1.8        | Congested                  |
| Blocked  | 10.0       | Road closed / severe delay |

---

## 🗺️ System Workflow

1. User selects **start** and **end** coordinates.
2. The API loads the **Rajkot road network** from MongoDB.
3. The system adjusts weights based on current traffic data.
4. **Dijkstra’s algorithm** computes the optimal route.
5. The API returns:

   * Total distance
   * Estimated travel time
   * List of route coordinates
   * Optional traffic detours

---

## 🧰 Installation & Setup

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* [MongoDB](https://www.mongodb.com/try/download/community)
* Internet access (for Overpass API on first run)

### Clone the Repository

```bash
git clone https://github.com/<your-username>/SmartRouteRajkot.git
cd SmartRouteRajkot/SmartRoute.API
```

### Configure MongoDB

Edit **appsettings.json**:

```json
"ConnectionStrings": {
  "MongoDB": "mongodb://localhost:27017"
}
```

Or use **User Secrets** (recommended):

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  }
}
```

### Run the API

```bash
dotnet restore
dotnet build
dotnet run
```

Access Swagger at:

```
https://localhost:5001/swagger
```

---

## 🔌 API Endpoints

### 📍 Route API

**POST** `/api/routes/calculate`
**Body:**

```json
{
  "startLat": 22.3039,
  "startLng": 70.8022,
  "endLat": 22.3100,
  "endLng": 70.8000,
  "avoidTraffic": true
}
```

**Response:**

```json
{
  "totalDistance": 2.4,
  "estimatedTime": 3.6,
  "hasTrafficDetours": false,
  "path": [
    { "latitude": 22.3041, "longitude": 70.8020 },
    { "latitude": 22.3062, "longitude": 70.8013 }
  ]
}
```

---

### 🚦 Traffic API

**POST** `/api/traffic/report`
Report new traffic data:

```json
{
  "roadName": "Kalavad Road",
  "trafficLevel": 3,
  "latitude": 22.30,
  "longitude": 70.79,
  "reportedBy": "User"
}
```

**GET** `/api/traffic/current`
Fetch current traffic data in Rajkot.

---

## 📊 Database Schema (MongoDB)

**Collection:** `roads`

```json
{
  "_id": "abc123",
  "type": "Feature",
  "geometry": {
    "type": "LineString",
    "coordinates": [[70.8001, 22.3021], [70.8022, 22.3054]]
  },
  "properties": {
    "name": "Kalavad Road",
    "highway": "primary",
    "maxspeed": "60",
    "lanes": "2"
  }
}
```

**Collection:** `traffic`

```json
{
  "roadName": "Kalavad Road",
  "trafficLevel": 2,
  "multiplier": 1.2,
  "reportedAt": "2025-11-07T10:00:00Z",
  "coordinates": [70.800, 22.304]
}
```

---

## 🧠 How It Works (Step-by-Step)

1. On first run, the app downloads Rajkot road data from **Overpass API**.
2. It stores the data as **GeoJSON-like documents** in MongoDB.
3. Roads are converted to graph nodes and edges.
4. Dijkstra’s algorithm finds the shortest path between two coordinates.
5. ETA is adjusted based on **traffic multipliers**.
6. Results are returned via REST API (JSON format).

---

## 📈 Future Enhancements

* Add **A*** algorithm for faster route finding.
* Integrate **real map visualization** via Leaflet or Mapbox.
* Include **turn-by-turn navigation instructions**.
* Extend to multiple cities with dynamic bounding box.

---

## 📜 License

This project is licensed under the **MIT License**.

---

## 👨‍💻 Author

**Faham Khatri**
📍 Rajkot, India
🌐 [GitHub Profile](https://github.com/Faham1111)

---

✨ *"Smart navigation starts with smart algorithms."* ✨
