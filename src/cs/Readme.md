# Azure IoT Edge Module to read BME280 sensor data

With the lack of working libraries for this sensor in .NET core I over engineered a solution that uses a Python module that reads the information for the sensor.

This solution is simple, but no recommended for production, because IoT Edge modules are deployed as Docker images.

All I had to do was to make sure I include Python and the necessary libraries into my docker image.

# Getting the tools you need

Complete information can be found[here](https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-csharp-module).

1. dotnet new aziotedgemodule -n AzureIoTEdge.BME280Module -o .
2. 

# Lessons learned

- If you intend to deploy to a Raspberry Pi make sure you use .NET Core base image that is compatible with ARM (microsoft/dotnet:2.0.0-runtime-stretch-arm32v7)
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
   - If you update the module docker image in the container registry just make a docker pull {image} in Raspberry Pi which will automatically restart the module with the new image


