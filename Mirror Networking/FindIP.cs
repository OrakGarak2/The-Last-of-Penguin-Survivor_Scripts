using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;

public class FindIP : MonoBehaviour
{
    void Start()
    {
        TMP_Text myIPText = GetComponent<TMP_Text>();

        IPHostEntry hostEntry =  Dns.GetHostEntry(Dns.GetHostName());

        if (hostEntry == null) return;

        foreach(IPAddress ip in hostEntry.AddressList)
        {
            if(ip.AddressFamily == AddressFamily.InterNetwork)
            {
                myIPText.text = "IP: " + ip.ToString();
            }
        }
    }
}
