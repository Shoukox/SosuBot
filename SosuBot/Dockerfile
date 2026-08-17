# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Build the stateless rosu-pp C ABI wrapper on Debian/glibc. The Rust SDK does
# not flow into the final image.
FROM rust:1.97.1-bookworm AS native-build
WORKDIR /src/rosu-pp-native
COPY ["SosuBot.PerformanceCalculator/Calculators/Rosu/native/rosu-pp-native/Cargo.toml", "SosuBot.PerformanceCalculator/Calculators/Rosu/native/rosu-pp-native/Cargo.lock", "SosuBot.PerformanceCalculator/Calculators/Rosu/native/rosu-pp-native/rust-toolchain.toml", "./"]
COPY ["SosuBot.PerformanceCalculator/Calculators/Rosu/native/rosu-pp-native/src", "./src"]
RUN --mount=type=cache,target=/usr/local/cargo/registry \
    --mount=type=cache,target=/usr/local/cargo/git \
    cargo build --release --locked

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SosuBot/SosuBot.csproj", "SosuBot/"]
COPY ["SosuBot.Database/SosuBot.Database.csproj", "SosuBot.Database/"]
COPY ["SosuBot.Graphics/SosuBot.Graphics.csproj", "SosuBot.Graphics/"]
COPY ["SosuBot.Localization/SosuBot.Localization.csproj", "SosuBot.Localization/"]
COPY ["SosuBot.PerformanceCalculator/SosuBot.PerformanceCalculator.csproj", "SosuBot.PerformanceCalculator/"]
COPY ["tests/SosuBot.PerformanceCalculator.Smoke/SosuBot.PerformanceCalculator.Smoke.csproj", "tests/SosuBot.PerformanceCalculator.Smoke/"]
RUN dotnet restore "./SosuBot/SosuBot.csproj" \
    && dotnet restore "./tests/SosuBot.PerformanceCalculator.Smoke/SosuBot.PerformanceCalculator.Smoke.csproj"
COPY . .
COPY --from=native-build /src/rosu-pp-native/target/release/librosu_pp_native.so /src/SosuBot.PerformanceCalculator/Calculators/Rosu/runtimes/linux-x64/native/librosu_pp_native.so
WORKDIR "/src/SosuBot"
RUN dotnet build "./SosuBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SosuBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Runnable smoke target that uses the same runtime base and native asset as the
# production image. CI builds and runs this target directly.
FROM build AS calculator-smoke-publish
WORKDIR /src
RUN dotnet publish "./tests/SosuBot.PerformanceCalculator.Smoke/SosuBot.PerformanceCalculator.Smoke.csproj" \
    -c Release -o /app/calculator-smoke /p:UseAppHost=false --no-restore

FROM base AS calculator-smoke
WORKDIR /app
COPY --from=calculator-smoke-publish /app/calculator-smoke .
ENTRYPOINT ["dotnet", "SosuBot.PerformanceCalculator.Smoke.dll"]

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SosuBot.dll"]
