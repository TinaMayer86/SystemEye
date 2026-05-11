async function fetchSensors() {
    try {
        const response = await fetch('/sensors');

        if (!response.ok) throw new Error('API Fehler');

        const data = await response.json();
        document.getElementById('error-msg').style.display = 'none';
        renderDashboard(data);

    } catch (error) {
        console.error("Fehler beim Abrufen:", error);
        document.getElementById('error-msg').style.display = 'block';
    }
}

function renderDashboard(sensors) {
    const dashboard = document.getElementById('dashboard');
    dashboard.innerHTML = ''; // Altes Dashboard leeren

    const groupedSensors = {};
    sensors.forEach(sensor => {
        const hwType = sensor.hardwareType || 'Unbekannt';
        if (!groupedSensors[hwType]) {
            groupedSensors[hwType] = [];
        }
        groupedSensors[hwType].push(sensor);
    });

    for (const [hwType, sensorList] of Object.entries(groupedSensors)) {

        const card = document.createElement('div');
        card.className = 'hardware-card';

        const title = document.createElement('div');
        title.className = 'hardware-title';
        title.innerText = hwType;
        card.appendChild(title);

        sensorList.forEach(sensor => {
            const item = document.createElement('div');
            item.className = 'sensor-item';

            const name = document.createElement('span');
            name.className = 'sensor-name';
            name.innerText = sensor.name;

            const value = document.createElement('span');
            value.className = 'sensor-value';

            const numValue = Number(sensor.value).toFixed(1);
            value.innerText = `${numValue} ${sensor.format}`;

            item.appendChild(name);
            item.appendChild(value);
            card.appendChild(item);
        });

        dashboard.appendChild(card);
    }
}

setInterval(fetchSensors, 1000);
fetchSensors();