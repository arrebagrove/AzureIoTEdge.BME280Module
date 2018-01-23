FROM microsoft/dotnet:2.0.0-runtime-stretch-arm32v7

# Install Adafruit Python dependencies
RUN apt-get update && apt-get install -y build-essential \
    python-pip \
    python-dev \
    python-smbus \
    git \
    --no-install-recommends

# Install required Python library GPIO
RUN git clone https://github.com/adafruit/Adafruit_Python_GPIO.git
RUN cd Adafruit_Python_GPIO && python setup.py install && cd ..

# Clone Python repository
RUN git clone https://github.com/fbeltrao/Adafruit_Python_BME280.git
RUN cd Adafruit_Python_BME280 && python setup.py install

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "AzureIoTEdge.BME280Module.dll"]