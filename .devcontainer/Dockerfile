FROM mcr.microsoft.com/devcontainers/dotnet:0-7.0

# [Optional] Uncomment this section to install additional OS packages.
# RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
#     && apt-get -y install --no-install-recommends <your-package-list-here>

WORKDIR /workspaces/WriteFluency

RUN mkdir -p tools

RUN dotnet tool install dotnet-ef --tool-path /tools --version 7.0.5

# [Optional] Uncomment this line to install global node packages.
# RUN su vscode -c "source /usr/local/share/nvm/nvm.sh && npm install -g <your-package-here>" 2>&1

#Git hub codespace log creation file: /workspaces/.codespaces/.persistedshare
