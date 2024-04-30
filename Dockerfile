
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/EventHubSolution.BackendServer/EventHubSolution.BackendServer.csproj", "EventHubSolution.BackendServer/"]
COPY ["src/EventHubSolution.ViewModels/EventHubSolution.ViewModels.csproj", "EventHubSolution.ViewModels/"]
RUN dotnet restore "EventHubSolution.BackendServer/EventHubSolution.BackendServer.csproj"
COPY . .
WORKDIR "/src/src/EventHubSolution.BackendServer"
RUN dotnet build "EventHubSolution.BackendServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EventHubSolution.BackendServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EventHubSolution.BackendServer.dll"]