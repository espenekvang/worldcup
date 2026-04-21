# Stage 1: Frontend build
FROM node:20-alpine AS frontend-build

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY src/ ./src/
COPY index.html ./
COPY vite.config.ts ./
COPY tsconfig*.json ./

ARG VITE_GOOGLE_CLIENT_ID
ARG VITE_APP_VERSION=dev
ENV VITE_API_URL=""

RUN VITE_GOOGLE_CLIENT_ID=${VITE_GOOGLE_CLIENT_ID} \
    VITE_APP_VERSION=${VITE_APP_VERSION} \
    VITE_API_URL="" \
    npm run build

# Stage 2: Backend build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build

WORKDIR /src

COPY api/WorldCup.Api/ ./

RUN dotnet publish -c Release -o /publish

# Copy React build output into wwwroot
COPY --from=frontend-build /app/dist /publish/wwwroot/

# Copy match and team data to expected runtime location
COPY src/data/matches.json /publish/data/matches.json
COPY src/data/teams.json /publish/data/teams.json

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

COPY --from=backend-build /publish /app

WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

RUN mkdir -p /app/data

EXPOSE 8080

ENTRYPOINT ["dotnet", "WorldCup.Api.dll"]
