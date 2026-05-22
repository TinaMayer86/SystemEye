# 👁️ SystemEye

Eine moderne, leichtgewichtige Hardware-Überwachungsanwendung für Windows. **SystemEye** kombiniert eine schicke Desktop-Oberfläche in **WPF (Material Design)** mit einer integrierten **ASP.NET Core REST-API** und einem **Web-Dashboard**, um deine Systemdaten (CPU, GPU, RAM, Speicher, Netzwerk) in Echtzeit zu überwachen, historisch in einer **SQLite-Datenbank** zu aggregieren und im Netzwerk bereitzustellen.

---

## ✨ Features

- **Echtzeit-Desktop-Dashboard:** Kompakte Windows-Anwendung mit reaktiven Live-Graphen (unter Verwendung von *ScottPlot*) im modernen Material Design 3 Look.
- **Integrierte REST-API & Webserver:** Startet auf Wunsch einen lokalen Kestrel-Webserver, über den die Sensordaten plattformunabhängig im gesamten lokalen Netzwerk (LAN/WLAN) als JSON abgerufen werden können.
- **Web-Dashboard:** Ein integriertes, responsives HTML5/JS-Dashboard zur Live-Ansicht der Sensoren im Browser (z. B. auf dem Smartphone, Tablet oder Zweitmonitor).
- **Langzeit-Historie:** Sekündlich erfasste Sensordaten werden minütlich aggregiert (Min, Max, Durchschnitt) und vollautomatisch in einer lokalen SQLite-Datenbank verschlüsselt/gespeichert.
- **Flexibles Sensor-Management:** Jedes Hardware-Signal kann in den Einstellungen einzeln aktiviert oder deaktiviert werden, um nur das zu sehen, was dich interessiert.
- **Daten-Export:** Exportiert die gesammelten Statistiken der letzten Stunde per Knopfdruck in eine saubere Textdatei.

---

## 🏗️ Architektur & Technologien

Das Projekt folgt einer sauberen, entkoppelten MVVM-Architektur (Model-View-ViewModel) und setzt auf moderne .NET-Bibliotheken:

- **Frontend (Desktop):** WPF (Windows Presentation Foundation) mit .Net 10.0
- **UI-Design:** [Material Design In XAML Toolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) (Theme-Unterstützung für Light/Dark)
- **MVVM-Framework:** [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (Source Generator für Properties, Commands und WeakReferenceMessenger)
- **Hardware-Schnittstelle:** [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (Direktes Auslesen von CPU-Temperaturen, Lüfterdrehzahlen, Taktraten & Spannungen)
- **Charts/Diagramme:** [ScottPlot.WPF](https://scottplot.net/) (Performante Signal-Graphen ohne UI-Stottern)
- **Backend & API:** ASP.NET Core Minimal APIs (Kestrel)
- **Datenbank:** SQLite (Microsoft.Data.Sqlite) für wartungsfreie, dateibasierte Speicherung

---

## 🚀 Erste Schritte / Installation

### Voraussetzungen
Da die Anwendung hardwarenah arbeitet, benötigt sie **Administratorrechte** unter Windows, um sensible Sensoren wie CPU-Temperaturen und Spannungen direkt aus den Registern der Hardware auszulesen.

### Ausführen über Visual Studio
1. Klone das Repository:
   git clone [https://github.com/DEIN-BENUTZERNAME/SystemEye.git](https://github.com/DEIN-BENUTZERNAME/SystemEye.git)
