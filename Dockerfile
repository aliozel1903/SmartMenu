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

ENV ASPNETCORE_ENVIRONMENT=Production
# Sunucu Londra'da ve varsayılan saat dilimi UTC. Bu olmadan sipariş saatleri ve fişler 3 saat geri,
# gece 00:00-03:00 arasındaki menü ziyaretleri de bir önceki güne yazılırdı.
ENV TZ=Europe/Istanbul
EXPOSE 8080

# Render gibi platformlar dinlenecek portu PORT ortam değişkeniyle bildirir.
# PORT tanımlı değilse (ör. yerelde docker run) .NET'in varsayılanı olan 8080 kullanılır.
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet SmartMenu.dll"]
