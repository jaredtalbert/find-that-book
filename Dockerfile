FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

RUN apt-get update \
    && apt-get install --yes --no-install-recommends python3 python-is-python3 \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet workload install wasm-tools

COPY ["FindThatBook.Client/FindThatBook.Client.csproj", "FindThatBook.Client/"]
COPY ["FindThatBook.Server/FindThatBook.Server.csproj", "FindThatBook.Server/"]

RUN dotnet restore "FindThatBook.Client/FindThatBook.Client.csproj"
RUN dotnet restore "FindThatBook.Server/FindThatBook.Server.csproj"

COPY . .

RUN dotnet publish "FindThatBook.Client/FindThatBook.Client.csproj" \
    --configuration Release \
    /p:EmccLinkOptimizationFlag=-O0 \
    /p:WasmRunWasmOpt=false

RUN dotnet publish "FindThatBook.Server/FindThatBook.Server.csproj" \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

RUN mkdir -p /app/publish/wwwroot
RUN cp -R \
    "FindThatBook.Client/bin/Release/net10.0-browser/publish/wwwroot/." \
    /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "FindThatBook.Server.dll"]
