# Kubernetes Manifests for CinemaServicesManager

This directory contains Kubernetes manifests for deploying the solution:

- `deployment-gateway.yaml`: Gateway API Deployment & Service
- `deployment-showtimes.yaml`: Showtimes API Deployment & Service
- `deployment-movies.yaml`: Movies API Deployment & Service
- `deployment-application.yaml`: Application API Deployment & Service
- `ingress.yaml`: Ingress resource for unified routing

## Usage

1. Build and push Docker images for each service (`gateway-api`, `showtimes-api`, `movies-api`, `application-api`).
2. Apply manifests:
   ```sh
   kubectl apply -f k8s/
   ```
3. Ensure an ingress controller (e.g., NGINX) is installed in your cluster.
