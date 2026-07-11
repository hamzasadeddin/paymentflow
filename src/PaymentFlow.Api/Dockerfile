FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY src/PaymentFlow.Domain/PaymentFlow.Domain.csproj src/PaymentFlow.Domain/
COPY src/PaymentFlow.Application/PaymentFlow.Application.csproj src/PaymentFlow.Application/
COPY src/PaymentFlow.Infrastructure/PaymentFlow.Infrastructure.csproj src/PaymentFlow.Infrastructure/
COPY src/PaymentFlow.Api/PaymentFlow.Api.csproj src/PaymentFlow.Api/
RUN dotnet restore src/PaymentFlow.Api/PaymentFlow.Api.csproj
COPY src/ src/
RUN dotnet publish src/PaymentFlow.Api/PaymentFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentFlow.Api.dll"]
