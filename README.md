# Cinema Services Manager Solution

## Overview

Cinema Services Manager is a modular, microservices-based solution for managing cinema operations, including showtimes, movies, and API gateway orchestration. The solution is designed for scalability, maintainability, and cloud-native deployment, supporting both Azure and AWS infrastructures. It leverages modern .NET Core, Docker, and Kubernetes, and follows best practices in software architecture and DevOps.

---

## Architecture Style

- **Microservices Architecture**: Each domain (Showtimes, Movies, Gateway, Application) is implemented as an independent service with its own API, database, and business logic.
- **API Gateway Pattern**: Centralized entry point for all client requests, handling routing, authentication, and aggregation.
- **Clean Architecture / Onion Architecture**: Separation of concerns between domain, application, and infrastructure layers.
- **CQRS (Command Query Responsibility Segregation)**: Commands and queries are handled separately for scalability and clarity.
- **Dependency Injection**: All services and repositories are injected for testability and flexibility.
- **Middleware Pipeline**: Custom middleware for error handling, logging, and request timing.

![image_solution_structure](https://github.com/DenisKovalyonokSamples/CinemaServicesManager/blob/main/SolutionStructure.png)

## Project Structure

```
/CNM.Application           # Core application logic, use cases, middleware, authentication
/CNM.Domain               # Domain entities, interfaces, repositories
/CNM.Showtimes.API        # Showtimes microservice (API, controllers, startup)
/CNM.Movies.API           # Movies microservice (API, controllers, startup)
/CNM.Gateway.API          # API Gateway (routing, aggregation, authentication)
/CNM.Application.Tests    # Unit and integration tests for Application layer
/CNM.Showtimes.Tests      # Unit and integration tests for Showtimes API
/CNM.Movies.Tests         # Unit and integration tests for Movies API
/CNM.Gateway.Tests        # Unit and integration tests for Gateway API
/docker-compose.yml       # Multi-container orchestration for local development
/k8s/                     # Kubernetes manifests (deployment, service, ingress, config)
/Details.txt              # Initial requirements and task description
```

---

## Technical Stack

- **.NET Core 3.1 / C# 8.0**: Main application and APIs
- **Entity Framework Core**: Data access and in-memory testing
- **ASP.NET Core MVC**: API controllers and middleware
- **Docker**: Containerization for all services
- **Kubernetes**: Orchestration for cloud-native deployments
- **Azure/AWS**: Cloud deployment targets
- **XUnit**: Unit and integration testing
- **Swagger/OpenAPI**: API documentation (if enabled)
- **Health Checks**: Built-in health endpoints for monitoring

### Third-Party Libraries

- `Microsoft.EntityFrameworkCore`
- `Microsoft.AspNetCore.*`
- `Xunit`
- `Swashbuckle.AspNetCore` (if Swagger is enabled)
- `Microsoft.Extensions.*` (DI, Logging, Configuration, HealthChecks)

---

## Patterns & Practices

- **Repository Pattern**: Abstracts data access for testability and flexibility.
- **Custom Middleware**: For error handling (RFC7807 ProblemDetails), request timing, and logging.
- **Authentication Handler**: Custom token-based authentication for APIs.
- **Health Checks**: For liveness/readiness probes in Docker/Kubernetes.
- **Separation of Concerns**: Clear boundaries between domain, application, and infrastructure.

---

## Docker Functionality

- Each service has its own `Dockerfile` for building a self-contained image.
- `docker-compose.yml` orchestrates all services, networks, and dependencies for local development.
- Health checks and ports are exposed for each service.
- Environment variables are used for configuration.

### Docker Files

- `CNM.Showtimes.API/Dockerfile`
- `CNM.Movies.API/Dockerfile`
- `CNM.Gateway.API/Dockerfile`
- `CNM.Application/Dockerfile`
- `.dockerignore`
- `docker-compose.yml`

---

## Kubernetes Functionality

- All services are described with Kubernetes manifests in the `/k8s` directory.
- Includes `Deployment`, `Service`, and `Ingress` resources for each microservice.
- ConfigMaps and Secrets for environment configuration.
- Health checks mapped to Kubernetes liveness/readiness probes.

### Kubernetes Files

- `/k8s/showtimes-deployment.yaml`
- `/k8s/movies-deployment.yaml`
- `/k8s/gateway-deployment.yaml`
- `/k8s/application-deployment.yaml`
- `/k8s/ingress.yaml`
- `/k8s/configmap.yaml`
- `/k8s/secrets.yaml`

---

## Solution Startup Process

### Local Development (Docker + Visual Studio)

1. **Clone the Repository**
```sh
git clone https://github.com/DenisKovalyonokSamples/CinemaServicesManager.git
cd CinemaServicesManager
```

2. **Open Solution in Visual Studio**
   - Open `CinemaServicesManager.sln`.

3. **Build the Solution**
   - Restore NuGet packages and build all projects.

4. **Start All Services with Docker Compose**
   - Right-click `docker-compose` in Solution Explorer and select "Set as Startup Project".
   - Press `F5` or click "Start Debugging".
   - Alternatively, run in terminal:
```sh
 docker-compose up --build
```

5. **Access Services**
   - Gateway API: `http://localhost:8080`
   - Showtimes API: `http://localhost:5001`
   - Movies API: `http://localhost:5002`
   - Application API: `http://localhost:5003`

---

## CI/CD Process

### Azure DevOps Pipeline

1. **Build Pipeline**
   - Restore, build, and test all projects.
   - Build Docker images for each service.
   - Push images to Azure Container Registry (ACR).

2. **Release Pipeline**
   - Deploy images to Azure Kubernetes Service (AKS) using `/k8s` manifests.
   - Run database migrations if needed.
   - Health check endpoints for readiness.

3. **Pipeline YAML Example**
   - See `.azure-pipelines.yml` (if present) or create with steps:
     - `dotnet restore`
     - `dotnet build`
     - `dotnet test`
     - `docker build`
     - `docker push`
     - `kubectl apply -f k8s/`

### AWS CodePipeline

1. **Build Stage**
   - Use AWS CodeBuild to restore, build, and test.
   - Build Docker images and push to Amazon ECR.

2. **Deploy Stage**
   - Use AWS CodeDeploy or EKS to deploy images using `/k8s` manifests.

3. **Pipeline Steps**
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test`
   - `docker build`
   - `docker push`
   - `kubectl apply -f k8s/`

---

## Kubernetes Deployment

1. **Configure kubectl for your cluster** (AKS/EKS)
2. **Apply Configurations**
```sh
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/showtimes-deployment.yaml
kubectl apply -f k8s/movies-deployment.yaml
kubectl apply -f k8s/gateway-deployment.yaml
kubectl apply -f k8s/application-deployment.yaml
kubectl apply -f k8s/ingress.yaml
```
3. **Monitor Pods and Services**
```sh
kubectl get pods
kubectl get services
kubectl get ingress
```

---

## Testing

### Unit and Integration Tests

- **Unit Tests**: Cover core logic, middleware, repositories, and controllers.
- **Integration Tests**: Validate end-to-end API flows, database interactions, and service orchestration.

#### Test Projects

- `CNM.Application.Tests`
- `CNM.Showtimes.Tests`
- `CNM.Movies.Tests`
- `CNM.Gateway.Tests`

#### Running Tests Locally

1. **From Visual Studio**
   - Open Test Explorer and run all tests.

2. **From Command Line**
```sh
dotnet test
```

#### Adding Tests to CI/CD

- Ensure `dotnet test` is included in your pipeline YAML before build/publish steps.
- Example (Azure DevOps):
```yaml
- task: DotNetCoreCLI@2
    inputs:
      command: 'test'
      projects: '**/*.Tests.csproj'
```
- Example (AWS CodeBuild):
```yaml
build:
    commands:
      - dotnet test
```

---

## Initial Task

The solution implements the requirements described in `Details.txt` in the root folder. Please refer to this file for the original task and acceptance criteria.

---

## References

- [.NET Core Documentation](https://docs.microsoft.com/dotnet/core/)
- [Docker Documentation](https://docs.docker.com/)
- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [Azure DevOps Pipelines](https://docs.microsoft.com/azure/devops/pipelines/)
- [AWS CodePipeline](https://docs.aws.amazon.com/codepipeline/)

---

## Contact

For questions or contributions, please open an issue or contact the repository owner.

---

This description provides a clear, professional, and detailed overview for HR, architects, and senior developers reviewing your GitHub repository.