# Plataforma de Créditos - Parcial 2026-1

Este proyecto es una aplicación web ASP.NET MVC para la gestión de solicitudes de crédito.

## Requisitos
- .NET 10 SDK
- Redis (para sesiones y caché)
- SQLite (base de datos local)

## Configuración Local

1. **Clonar el repositorio.**
2. **Configurar Redis**: Asegúrate de tener una instancia de Redis corriendo y actualiza la cadena de conexión en `appsettings.json` o mediante variables de entorno.
3. **Migraciones**:
   ```bash
   dotnet ef database update
   ```
4. **Ejecutar**:
   ```bash
   dotnet run
   ```

## Variables de Entorno Requeridas

Para despliegue (Docker/Render), asegúrate de configurar las siguientes variables:

- `ASPNETCORE_ENVIRONMENT`: `Production` o `Development`.
- `ASPNETCORE_URLS`: `http://*:$PORT` (manejado por el Dockerfile).
- `ConnectionStrings__DefaultConnection`: Cadena de conexión a la base de datos (ej: `DataSource=app.db`).
- `Redis__ConnectionString`: Cadena de conexión a Redis Cloud (ej: `redis-11967.c277.us-east-1-3.ec2.cloud.redislabs.com:11967,password=...,ssl=false,abortConnect=false,connectTimeout=10000,syncTimeout=10000`).

## Despliegue en Render

1. Crea un nuevo **Web Service** en Render.
2. Conecta tu repositorio de GitHub.
3. Selecciona **Docker** como el Runtime.
4. Render detectará automáticamente el `Dockerfile` en la raíz.
5. Configura las **Variables de Entorno** mencionadas arriba en el panel de Render.

**URL de Despliegue**: [PENDIENTE - Agrega tu URL aquí después del despliegue]

## Características Implementadas
- Gestión de Clientes y Solicitudes.
- Panel de Analista con reglas de negocio.
- Caché distribuido y sesiones con Redis.
- Autenticación y Autorización con Identity.
- Dockerización lista para producción.
