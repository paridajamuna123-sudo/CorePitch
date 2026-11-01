# Stage 1: Build Angular Frontend
FROM node:20-alpine AS client-build
WORKDIR /app/client
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

# Stage 2: Build .NET Backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS server-build
WORKDIR /app
COPY src/JobMailer.Domain/JobMailer.Domain.csproj src/JobMailer.Domain/
COPY src/JobMailer.Application/JobMailer.Application.csproj src/JobMailer.Application/
COPY src/JobMailer.Infrastructure/JobMailer.Infrastructure.csproj src/JobMailer.Infrastructure/
COPY src/JobMailer.Api/JobMailer.Api.csproj src/JobMailer.Api/
RUN dotnet restore src/JobMailer.Api/JobMailer.Api.csproj

COPY src/ src/
WORKDIR /app/src/JobMailer.Api
RUN dotnet publish -c Release -o /app/publish

# Copy Angular bundle into wwwroot
COPY --from=client-build /app/client/dist/client/browser /app/publish/wwwroot

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=server-build /app/publish ./

# Create persistent storage directories for SQLite and Resumes
RUN mkdir -p /app/App_Data/Resumes

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "JobMailer.Api.dll"]
