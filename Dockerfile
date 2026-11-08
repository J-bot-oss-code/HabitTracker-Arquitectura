# ---------- Etapa 1: compilar y publicar ----------
# La imagen del SDK (grande) solo se usa aquí; no llega a la imagen final.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Se copia el repositorio (.dockerignore deja fuera bin/, obj/, .git/, frontend/, tests/ y .env).
COPY . .

# La restauración de paquetes depende de la red (NuGet): un corte puntual no debe tumbar el build, así que se reintenta.
RUN for intento in 1 2 3; do \
      dotnet restore Host/WebApi/WebApi.csproj && exit 0; \
      echo "dotnet restore falló (intento $intento de 3); reintentando..."; sleep 5; \
    done; exit 1

RUN dotnet publish Host/WebApi/WebApi.csproj -c Release -o /app/publish --no-restore

# ---------- Etapa 2: imagen final, solo el runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Parches de seguridad del sistema base. La imagen oficial de Microsoft se reconstruye con retraso respecto
# a los avisos de Ubuntu, y el CI (Trivy) bloquea las vulnerabilidades altas con parche disponible (RD-10).
# Se hace como root, antes de "USER app", y se borra la caché de apt para no engordar la imagen.
RUN apt-get update \
    && apt-get upgrade -y --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*

# Carpeta de los archivos de documentos. En docker-compose.yml se monta aquí un VOLUMEN CON NOMBRE:
# lo que se guarda en esta ruta vive fuera de la imagen y sobrevive a destruir el contenedor (RF-DOC-05).
# Se crea con dueño "app" para que el volumen nuevo herede ese dueño y el proceso pueda escribir.
RUN mkdir -p /app/data/documentos /app/data/claves && chown -R app:app /app/data

COPY --from=build /app/publish .

# No se ejecuta como root.
USER app

# La aplicación escucha en el 8080 dentro del contenedor. Qué puerto del equipo la expone lo decide el compose.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Aquí NO hay claves ni cadenas de conexión: llegan por variables de entorno desde el compose (RD-10).
ENTRYPOINT ["dotnet", "WebApi.dll"]
