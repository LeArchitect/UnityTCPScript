using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

public class TCPServerScript : MonoBehaviour
{
    [SerializeField]
    public int port = 5005;
    public string IP= "192.168.1.37";

    public static int runningNumber = 0;
    public TcpListener server;

    // All the available device models
    public static List<GameObject> availableDevices = new List<GameObject>();
    public static List<SpawnHandlerScript.Spawnable> onlineDevices = new List<SpawnHandlerScript.Spawnable>();

    // Testing values
    public GameObject Drone;

    // Start is called before the first frame update
    void Start()
    {
        Thread masterThread = new Thread(MasterThread);
        masterThread.Priority = System.Threading.ThreadPriority.Highest;
        Debug.Log("Program Started...!");
        masterThread.Start();
        Debug.Log("Server Started...!");

        // Testing values
        Drone = GameObject.Find("Drone");
        availableDevices.Add(Drone);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("escape"))
        {
            server.Stop();
            Application.Quit();
        }
    }

    // Gets the local ip address
    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            Debug.Log(ip);
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    // Thread to rule them all
    public void MasterThread()
    {
        IPAddress localAddr = IPAddress.Parse(GetLocalIPAddress());
        TcpListener server = new TcpListener(localAddr, port);
        server.Start();
        try
        {
            while (true)
            {
                Debug.Log("Waiting Connection...");
                TcpClient client = server.AcceptTcpClient();
                Debug.Log("Connection Established!");

                Thread connThread = new Thread(()=>ConnectionThread(client));
                connThread.Start();
            }
        }
        catch (SocketException e)
        {
            Debug.Log("SocketException: " + e);
            server.Stop();
        }
    }

    // Thread for every connection
    public void ConnectionThread(System.Object obj)
    {
        TcpClient client = (TcpClient)obj;
        var stream = client.GetStream();

        string data = null;
        Byte[] bytes = new Byte[1052];
        int i;

        try
        {
            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                string hex = BitConverter.ToString(bytes);
                data = Encoding.ASCII.GetString(bytes, 0, i);
                Debug.Log(Thread.CurrentThread.ManagedThreadId + ": Received: " + data);
                DataHandler(data, client);
            }
        }
        catch (SocketException e)
        {
            Debug.Log("SocketException: " + e);
            stream.Close();
        }
    }

    public void DataHandler(string data, TcpClient client)
    {
        //ADD ID ID.Class UWB Anchor ID.Pos X Y Z Yw P R ID.Posbase UWB ID.Mesh URL//
        string[] splitter = new string[] { " " };
        string[] IDsplitter = new string[] { "." };
        string[] strings = data.Split(splitter, StringSplitOptions.RemoveEmptyEntries);

        string deviceID;
        string posBase;
        string url;

        // Alias aka. IP address of the client // 
        // TODO: Hashing the information 
        string alias = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

        // Adding device for the first time //
        if (strings[0] == "ADD")
        {
            // DeviceID //
            deviceID = strings[1];

            // Classes //
            if(SpawnHandlerScript.classes.Count != 0)
                SpawnHandlerScript.classes.Clear();
            int i;
            for (i = 3; i < strings.Length - 1; i++)
            {
                if (strings[i] == (deviceID + ".pos"))
                    break;
                else
                {
                    SpawnHandlerScript.classes.Add(strings[i]);
                }
            }
            i = SpawnHandlerScript.classes.Count;

            // Location //
            SpawnHandlerScript.newDeviceLocation = new Vector3(float.Parse(strings[4 + i], CultureInfo.InvariantCulture), float.Parse(strings[5 + i], CultureInfo.InvariantCulture), float.Parse(strings[6 + i], CultureInfo.InvariantCulture));

            // Rotation //
            Vector3 rotation;
            rotation = new Vector3(float.Parse(strings[7 + i], CultureInfo.InvariantCulture), float.Parse(strings[8 + i], CultureInfo.InvariantCulture), float.Parse(strings[9 + i], CultureInfo.InvariantCulture));
            SpawnHandlerScript.newDeviceRotation = Quaternion.Euler(rotation);
            
            // Posbase and url //
            posBase = strings[11 + i];
            
            url = strings[13 + i];

            // Debug values //
            Debug.Log(deviceID);
            Debug.Log(SpawnHandlerScript.classes[0] + " " + SpawnHandlerScript.classes[1]);
            Debug.Log(SpawnHandlerScript.newDeviceLocation);
            Debug.Log(rotation);
            Debug.Log(posBase);
            Debug.Log(url);

            // DeviceID Alias PosBase URL //
            SpawnHandlerScript.newDeviceInfo = new List<string>{deviceID, alias, posBase, url};

            // Setting the flag to init the new device //
            SpawnHandlerScript.gotNewDevice = true;
        }
        // Updating existing device information //
        else
        {
            // ID.battery level // or // ID.pos x y z yw p r //
            string[] message = data.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
            string[] IDpart = message[0].Split(IDsplitter, StringSplitOptions.RemoveEmptyEntries);

            // deviceID //
            deviceID = IDpart[0];
            Debug.Log(deviceID);

            // Updating battery information //
            if(IDpart[1] == "battery")
            {
                // DeviceID //
                SpawnHandlerScript.updateDeviceID = deviceID;

                // battery level //
                SpawnHandlerScript.batteryLevel = message[1];

                // Setting battery update flag //
                SpawnHandlerScript.gotBatteryUpdate = true;
            }
            // Updating position //
            else if (IDpart[1] == "pos")
            {
                List<float> floats = new List<float>();
                
                int j;
                for (j = 0; j < (message.Length - 1); j++)
                {
                    float result = float.Parse(message[j + 1], CultureInfo.InvariantCulture);
                    floats.Add(result);
                }
                // Location //
                SpawnHandlerScript.updateLocation = new Vector3(floats[0], floats[1], floats[2]);

                // Rotation //
                Vector3 rotation = new Vector3(floats[3], floats[4], floats[5]);
                SpawnHandlerScript.updateRotation = Quaternion.Euler(rotation);

                // DeviceID //
                SpawnHandlerScript.updateDeviceID = deviceID;

                // Setting update flag //
                SpawnHandlerScript.gotDeviceUpdate = true;
            }
            // TODO Extra stuff //
            else
            {
                Debug.Log("Something extra is under TODO");
            }
        }
    }
}

