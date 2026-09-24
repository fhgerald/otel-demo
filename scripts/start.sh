#!/usr/bin/env bash
# Startet den minikube, baut die Images darin und rollt das Beispiel aus.
# Die Images werden bewusst IM minikube gebaut -- damit gibt es keine
# Registry und nichts wird waehrend der Vorlesung aus dem Netz gezogen.
set -euo pipefail

cd "$(dirname "$0")/.."

if ! command -v minikube >/dev/null; then
    echo "minikube fehlt. Installation: brew install minikube" >&2
    exit 1
fi

if [ ! -f k8s/otel-credentials.yaml ]; then
    echo "k8s/otel-credentials.yaml fehlt." >&2
    echo "Vorlage kopieren und Token eintragen:" >&2
    echo "  cp k8s/otel-credentials.example.yaml k8s/otel-credentials.yaml" >&2
    exit 1
fi

echo "==> minikube starten"
minikube status >/dev/null 2>&1 || minikube start --cpus=2 --memory=4096

echo "==> Images bauen"
# Auf dem Host bauen und anschliessend in den Cluster laden. Das ist
# unabhaengig von der Container-Laufzeit des minikube -- der Umweg ueber
# "minikube docker-env" funktioniert nur, wenn die Laufzeit Docker ist,
# und minikube verwendet standardmaessig containerd.
docker build -t otel-demo/billing-api:1.0.0        src/BillingApi
docker build -t otel-demo/metering-simulator:1.0.0 src/MeteringSimulator

echo "==> Images in den minikube laden"
minikube image load otel-demo/billing-api:1.0.0
minikube image load otel-demo/metering-simulator:1.0.0

echo "==> Ausrollen"
kubectl apply -f k8s/00-namespace.yaml
kubectl apply -f k8s/otel-credentials.yaml

# Optionaler Collector im Cluster: macht die Demo unabhaengig von dash0
# und vom Netz. Aufruf: ./scripts/start.sh --mit-collector
if [ "${1:-}" = "--mit-collector" ]; then
    echo "==> Lokalen Collector ausrollen"
    kubectl apply -f k8s/05-collector-lokal.yaml
fi

kubectl apply -f k8s/10-billing-api.yaml
kubectl apply -f k8s/20-metering-simulator.yaml

echo "==> Warten, bis die Dienste laufen"
if [ "${1:-}" = "--mit-collector" ]; then
    kubectl -n otel-demo rollout status deployment/otel-collector --timeout=180s
fi
kubectl -n otel-demo rollout status deployment/billing-api --timeout=180s
kubectl -n otel-demo rollout status deployment/metering-simulator --timeout=180s

echo
echo "Fertig. Der Simulator schickt alle zwei Sekunden einen Messwert."
echo
echo "Abrechnung abfragen:"
echo "  kubectl -n otel-demo port-forward svc/billing-api 8080:8080"
echo "  curl http://localhost:8080/abrechnung"
echo
echo "Logs:"
echo "  kubectl -n otel-demo logs -f deployment/metering-simulator"
