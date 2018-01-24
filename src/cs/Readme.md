# Azure IoT Edge Module to read BME280 sensor data

With the lack of working libraries for this sensor in .NET core I over engineered a solution that uses a Python module to read the information from the sensor.

This solution is simple and abuses the fact that IoT Edge modules are deployed as Docker images.

All I had to do was to make sure I include Python and the necessary libraries into my docker image.

```docker
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
```

# Creating an Azure IoT Edge Module

Complete information can be found [here](https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-csharp-module).

Short version
1. Install dotnet project template: dotnet new -i Microsoft.Azure.IoT.Edge.Module
1. dotnet new aziotedgemodule -n {ProjectName} 

# The code included in the sample

A nice feature of IoT Edge modules is that you specify a message pipeline (routes), so that the output of one module becomes the input for another one. Once you define an output as "$upstream" the message is sent to IoT Hub ingestion.

In this CSharp version I have 2 modules:

- BME280: that reads every 1 minute the sensor and outputs to "sensor". 
Docker image: fbeltrao/bme280module

- Middleware: that reads from BME280/outputs/sensor and writes to "$upstream"
Docker image: fbeltrao/middlewaremodule

In this example the middleware will add additional information to measurements that did not stayed within the defined threshold ranges. It will add an message property Alert=1 to make it easier in IoT Hub to create filters. 

This is how the routing looks like:
```json
{
  "routes": {
    "SensorToMiddleware": "FROM /messages/modules/bme280/outputs/sensor INTO BrokeredEndpoint(\"/modules/middleware/inputs/sensor\")",
    "MiddlewareToIoTHub": "FROM /messages/modules/middleware/outputs/middlewareoutput INTO $upstream"
  }
}
```



# Tips and Tricks

- If you intend to deploy to a Raspberry Pi make sure you use .NET Core base image is compatible with ARM (i.e. microsoft/dotnet:2.0.0-runtime-stretch-arm32v7)

- The sensor IoT Edge Module must run in privileged mode in order to read values from I2C. To do so in IoT Hub define the "Container Create Options" property as:
```json
    {
        "HostConfig": {
            "Privileged": "true"
        }
    }
```
- Image runs for a few seconds and dies. To see what is going on you can execute the image directly
```cmd
docker start -i {name of my module image}
```

- If you get error 'standard_init_linux.go:185: exec user process caused "exec format error"' make sure you have the correct base docker image

- Copy files w/ ssh
```cmd
scp /path/from pi@PIMACHINENAME:/home/pi/WHERE_TO
```
- Handy Docker Commands
    - Show docker logs in Linux: journalctl -u docker.service
    - See docker images running: docker stats --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.MemPerc}}\t{{.NetIO}}\t{{.BlockIO}}\t{{.PIDs}}"
    - Look at image logs (to keep watching add -f)
    ```cmd
    docker logs {image id}
    ```
    - Clear stopped containers
    ```
    docker rm $(docker ps -qa --no-trunc --filter "status=exited")
    ```
    - Cleanup images
    ```
    docker rmi $(docker images --filter "dangling=true" -q --no-trunc) -f
    ```
    - If you update the module docker image in the container registry just make a docker pull {image} in Raspberry Pi which will automatically restart the module with the new image


## Quick reference for iothub-explorer

1. Start with login
iothub-explorer login "HostName=xxxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxxx"
1. List devices: ```iothub-explorer list```
1. Monitor events 
```iothub-explorer monitor-events  --login "HostName=xxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxx"```
