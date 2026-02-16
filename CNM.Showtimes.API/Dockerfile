
FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY CNM.Showtimes.API/CNM.Showtimes.API.csproj CNM.Showtimes.API/
COPY CNM.Movies.API/CNM.Movies.API.csproj CNM.Movies.API/
COPY CNM.Gateway.API/CNM.Gateway.API.csproj CNM.Gateway.API/
COPY CNM.Application/CNM.Application.csproj CNM.Application/
COPY CNM.Domain/CNM.Domain.csproj CNM.Domain/
RUN dotnet restore CNM.Showtimes.API/CNM.Showtimes.API.csproj
COPY CNM.Showtimes.API/. CNM.Showtimes.API/
COPY CNM.Movies.API/. CNM.Movies.API/
COPY CNM.Gateway.API/. CNM.Gateway.API/
COPY CNM.Application/. CNM.Application/
COPY CNM.Domain/. CNM.Domain/
WORKDIR /src/CNM.Showtimes.API
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Development
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CNM.Showtimes.API.dll"]
