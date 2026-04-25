# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Copiar csproj y restaurar dependencias
COPY *.csproj ./
RUN dotnet restore

# Copiar todo lo demás y compilar
COPY . ./
RUN dotnet publish -c Release -o out

# Etapa 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

# Variables de entorno
ENV APP_NET_CORE=Parcial-2026-1.dll

# Comando para ejecutar la aplicación
# Render inyecta dinámicamente la variable $PORT
CMD ["sh", "-c", "ASPNETCORE_URLS=http://*:$PORT dotnet $APP_NET_CORE"]
