#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM ubuntu:21.04 AS final

EXPOSE 80
EXPOSE 443

ENV DEBIAN_FRONTEND=noninteractive 

WORKDIR /tmp

#install apks
RUN apt-get update -y
RUN apt-get install -y python3-pip python-dev build-essential sox
RUN apt-get install wget curl -y

#install dotnet sdk
RUN wget https://packages.microsoft.com/config/ubuntu/21.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN rm packages-microsoft-prod.deb

RUN apt-get update
RUN apt-get install -y apt-transport-https && \
  apt-get update && \
  apt-get install -y dotnet-sdk-5.0

#install deepspeech
RUN pip3 install deepspeech

#Download pre-trained English model files
#WORKDIR /src/Models/en
#RUN curl -LO https://github.com/mozilla/DeepSpeech/releases/download/v0.9.3/deepspeech-0.9.3-models.pbmm
#RUN curl -LO https://github.com/mozilla/DeepSpeech/releases/download/v0.9.3/deepspeech-0.9.3-models.scorer

RUN mkdir -p /output
RUN mkdir -p /input

#compile
WORKDIR /src
COPY ["DeepSpeechServerAPI/DeepSpeechServerAPI.csproj", "DeepSpeechServerAPI/"]
RUN dotnet restore "DeepSpeechServerAPI/DeepSpeechServerAPI.csproj"
COPY . .
WORKDIR "/src/DeepSpeechServerAPI"
RUN dotnet build "DeepSpeechServerAPI.csproj" -c Release -o /app/build

#publish
RUN dotnet publish "DeepSpeechServerAPI.csproj" -c Release -o /app/publish

#run app
WORKDIR /app
ENTRYPOINT ["dotnet", "DeepSpeechServerAPI.dll"]