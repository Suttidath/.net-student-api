# ---------- Stage 1: build ----------
# ใช้ SDK image ในการ restore + publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj ก่อนแล้ว restore เพื่อให้ Docker cache layer นี้ไว้
COPY StudentAPI/StudentAPI.csproj StudentAPI/
RUN dotnet restore StudentAPI/StudentAPI.csproj

# copy โค้ดที่เหลือแล้ว publish เป็นไฟล์พร้อมรัน
COPY . .
RUN dotnet publish StudentAPI/StudentAPI.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
# ใช้ image เล็กกว่า (ไม่มี SDK) เพื่อรันจริง
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# ให้ Kestrel ฟังที่ port 8080 ใน container
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "StudentAPI.dll"]
