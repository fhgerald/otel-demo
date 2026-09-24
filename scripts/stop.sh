#!/usr/bin/env bash
# Raeumt das Beispiel ab. Der minikube bleibt stehen.
set -euo pipefail
cd "$(dirname "$0")/.."
kubectl delete namespace otel-demo --ignore-not-found
echo "Namespace otel-demo entfernt. minikube laeuft weiter (minikube stop beendet ihn)."
