using UnityEngine;

[SerializeField]
public class RemoteDevice
{
    public string type;
    public string name;
    public string address;
    public string connectorPort;

    public RemoteDevice(string type, string name, string address)
    {
        this.type = type;
        this.name = name;
        this.address = address;
    }


    public RemoteDevice(string type, string name, string address, string connectorPort)
    {
        this.type = type;
        this.name = name;
        this.address = address;
        this.connectorPort = connectorPort;
    }

}