# 1. aşama: uygulamayı derle
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS derleme
WORKDIR /src

# Önce sadece proje dosyasını kopyalayıp paketleri indiriyoruz; kod değişse bile
# paketler değişmediyse bu katman önbellekten gelir ve derleme hızlanır.
COPY SmartMenu.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish SmartMenu.csproj -c Release -o /app --no-restore

# 2. aşama: sadece çalışma zamanını içeren küçük imaj
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=derleme /app ./

# .NET 8 imajları varsayılan olarak 8080 portunu dinler.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "SmartMenu.dll"]
