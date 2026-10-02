# ExpressionEngine playground: an interactive REPL with examples and the full manual.
#
#   docker build -t expression-engine .
#   docker run -it --rm expression-engine                       # the playground (needs -it for a terminal)
#   docker run --rm expression-engine "2 * pi * 10"             # evaluate one expression
#   docker run --rm expression-engine --manual                  # print the manual
#   docker run --rm expression-engine --examples                # list the examples

# ---- build ---------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# Copy only what the playground needs, so unrelated edits (tests, docs outside the manual) keep the layer cache warm.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ExpressionEngine/ src/ExpressionEngine/
COPY src/ExpressionEngine.Playground/ src/ExpressionEngine.Playground/
COPY docs/MANUAL.md docs/MANUAL.md

# The manual is embedded into the program at build time (see ExpressionEngine.Playground.csproj).
RUN dotnet publish src/ExpressionEngine.Playground -c Release -o /app --no-self-contained -p:UseAppHost=false

# ---- run -----------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

LABEL org.opencontainers.image.title="ExpressionEngine playground" \
      org.opencontainers.image.description="Interactive REPL, examples and manual for the ExpressionEngine library" \
      org.opencontainers.image.source="https://github.com/vbocan/expression-engine" \
      org.opencontainers.image.licenses="MIT"

# The expressions use invariant number parsing, so the culture data (ICU) is not needed.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    TERM=xterm-256color

COPY --from=build /app .
# The manual is also available as plain text:  docker run --rm --entrypoint cat expression-engine /app/docs/MANUAL.md
COPY docs/MANUAL.md docs/MANUAL.md

USER app
ENTRYPOINT ["dotnet", "expr.dll"]
