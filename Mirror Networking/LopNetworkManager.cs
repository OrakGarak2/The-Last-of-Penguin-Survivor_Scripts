// UnityEngine
using UnityEngine;

// Project
using Mirror;

public class LopNetworkManager : NetworkManager
{
    public MonsterList monsterList;

    public static bool isLoading = true;

    public static GameObject GetPlayer()
    {
        if(NetworkClient.localPlayer == null)
            return null;

        return NetworkClient.localPlayer.gameObject;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        monsterList = FindAnyObjectByType<MonsterList>();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        startPositions.Add(transform); // 생성 위치 설정

        #region 기존의 OnServerAddPlayer 코드
        Transform startPos = GetStartPosition();
        GameObject player = startPos != null
            ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
            : Instantiate(playerPrefab);

        player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";
        NetworkServer.AddPlayerForConnection(conn, player);
        #endregion

        foreach (Monster monster in monsterList.monsters)
        {
            PenguinBody penguin = player.GetComponent<PenguinBody>();
            if (penguin != null)
            {
                monster.penguins.Add(penguin);
            }
        }
    }
}