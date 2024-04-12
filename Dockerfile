FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/EventHubSolution.BackendServer/EventHubSolution.BackendServer.csproj", "EventHubSolution.BackendServer/"]
COPY ["src/EventHubSolution.ViewModels/EventHubSolution.ViewModels.csproj", "EventHubSolution.ViewModels/"]
RUN dotnet restore "EventHubSolution.BackendServer/EventHubSolution.BackendServer.csproj"
COPY . .
WORKDIR "/src/src/EventHubSolution.BackendServer"
RUN dotnet build "EventHubSolution.BackendServer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [ "dotnet", "EventHubSolution.BackendServer.dll" ]