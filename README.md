# OpenTelemetry-Demo — Zählpunkt und Abrechnung

Kleines verteiltes System aus zwei Diensten, vollständig mit OpenTelemetry
instrumentiert. Es dient zwei Zwecken:

1. **Live-Demo in der Vorlesung am 2. Oktober** (Gastvortrag dash0), lokal auf
   minikube — unabhängig von Cluster und Netz.
2. **Grundgerüst für den Laborteil ab 16. Oktober**: Die Anwendungsschicht, die
   jede Gruppe für ihre Domäne baut, beginnt hier.

Deshalb ist das Beispiel bewusst klein, aber nicht trivial: Zwei Dienste, damit
ein Trace mehr als eine Spanne hat, und die fachliche Sprache des Use Case.

---

## Die beiden Dienste

**`metering-simulator`** erzeugt alle zwei Sekunden einen Viertelstundenwert für
einen von drei Zählpunkten und schickt ihn per HTTP an die Billing-API. Das
Verbrauchsprofil ist grob an einen Haushalt angelehnt: nachts wenig, tagsüber mehr.

**`billing-api`** nimmt Messwerte entgegen, legt sie ab und beantwortet unter
`/abrechnung` eine Summenabfrage. Die Ablage ist bewusst nur eine Liste im
Arbeitsspeicher — das Beispiel soll Telemetrie zeigen, nicht Persistenz. Im Labor
tritt OctoMesh an diese Stelle.

## Was dabei an Telemetrie entsteht

| Art | Woher |
|---|---|
| Server-Spanne je HTTP-Anfrage | automatisch, ASP.NET-Core-Instrumentierung |
| Client-Spanne je ausgehendem Aufruf | automatisch, HttpClient-Instrumentierung |
| `Messwert erzeugen`, `Messwert verarbeiten`, `Abrechnung berechnen` | selbst gesetzt, eigene `ActivitySource` |
| Laufzeitmetriken (GC, Threads, Speicher) | automatisch, Runtime-Instrumentierung |
| `billing.messwerte.empfangen`, `billing.energie.kwh` | selbst gesetzt, eigener `Meter` |

Die Trace-Kette läuft über beide Dienste: Der Kontext wird über den
`traceparent`-Header weitergereicht, darum kümmert sich die HttpClient-
Instrumentierung von selbst.

**Eine Falle, die man beim ersten Mal garantiert baut:** Eigene `ActivitySource`
und eigener `Meter` werden zwar erzeugt, aber nur exportiert, wenn sie in
`Program.cs` mit `AddSource(...)` beziehungsweise `AddMeter(...)` registriert
sind. Fehlt das, sieht man die eigenen Spans und Metriken nirgends — ohne
Fehlermeldung.

---

## Starten

Voraussetzungen: Docker, kubectl, minikube (`brew install minikube`), .NET-10-SDK
nur für die lokale Entwicklung.

```bash
cp k8s/otel-credentials.example.yaml k8s/otel-credentials.yaml
# Endpunkt und Token aus dash0 eintragen (Settings -> Endpoints)

./scripts/start.sh
```

Das Skript startet den minikube, baut beide Images auf dem Host, lädt sie mit
`minikube image load` in den Cluster und rollt alles aus. Dadurch wird während der
Vorlesung nichts aus dem Netz gezogen.

Der Umweg über `minikube docker-env` funktioniert **nicht**: minikube verwendet
standardmäßig containerd, nicht Docker, und der Build schlägt dann mit einem
Buildkit-Fehler fehl.

Mit `./scripts/start.sh --mit-collector` wird zusätzlich ein OpenTelemetry-Collector
im Cluster ausgerollt. Er schreibt jede Spanne in sein Log:

```bash
kubectl -n otel-demo logs -f deployment/otel-collector
```

Damit läuft die Demo vollständig ohne dash0 und ohne Internet — als Rückfallebene,
wenn im Hörsaal das Netz streikt. In `k8s/otel-credentials.yaml` zeigt der Endpunkt
dann auf `http://otel-collector:4317`.

Abrechnung abfragen:

```bash
kubectl -n otel-demo port-forward svc/billing-api 8080:8080
curl http://localhost:8080/abrechnung
```

Abräumen: `./scripts/stop.sh`

---

## Ablauf der Demo

Die Reihenfolge entspricht den Schritten, die die Studierenden später im Labor
selbst gehen:

1. **Ohne Telemetrie.** Secret mit leerem Endpunkt ausrollen, Dienste laufen,
   in dash0 ist nichts zu sehen. Die Anwendung funktioniert trotzdem.
2. **Automatische Instrumentierung.** Endpunkt setzen, neu ausrollen — Traces
   erscheinen, ohne dass eine Zeile Code geändert wurde.
3. **Eigene Spans.** `Messwert verarbeiten` und `Abrechnung berechnen` zeigen,
   wie fachliche Schritte sichtbar werden und welche Attribute sinnvoll sind.
4. **Eigene Metrik.** `billing.energie.kwh` als Histogramm über die Zeit.
5. **Die Kette.** Ein Trace über beide Dienste: Client-Spanne des Simulators,
   Server-Spanne der API, darin der fachliche Span.
6. **Der Fehlerfall.** `kubectl -n otel-demo scale deployment/billing-api --replicas=0`
   — der Simulator läuft weiter, die Spans tragen einen Fehlerstatus. Das ist
   partieller Ausfall, sichtbar gemacht.

Schritt 6 ist der Übergang zur Security-Diskussion und zugleich der Rückbezug auf
Kapitel 1: Ein verteiltes System ist eines, in dem ein Teil ausfallen kann,
während der Rest weiterläuft.

---

## Hinweise für den Einsatz

Das Token gehört nicht ins Repository. `k8s/otel-credentials.yaml` ist per
`.gitignore` ausgenommen; eingecheckt ist nur die Vorlage.

Für die Vorlesung ein eigenes dash0-Dataset verwenden, nicht ein produktives
Konto — es werden Testdaten erzeugt, und die Ansicht wird projiziert.

Vor dem Termin einmal vollständig durchspielen und eine Aufzeichnung als
Rückfallebene anlegen. Eine Live-Demo, die am Netz scheitert, kostet zwanzig
Minuten und den Faden.

## Stand der Erprobung

Am 24. September 2026 vollständig auf minikube durchgespielt: beide Dienste
laufen, der Simulator speist die API, `/abrechnung` wächst, und der Collector
empfängt Spannen aus **beiden** Diensten. Je Messwert entsteht ein Trace aus vier
Spannen — `Messwert erzeugen`, die automatische Client-Spanne, die automatische
Server-Spanne und `Messwert verarbeiten` —, die Kontextweitergabe über
Dienstgrenzen funktioniert also.

Auch Schritt 6 ist geprüft: Nach `scale deployment/billing-api --replicas=0` läuft
der Simulator weiter, und die Spannen tragen `Status code: Error` mit der Meldung
`Connection refused (billing-api:8080)`.
