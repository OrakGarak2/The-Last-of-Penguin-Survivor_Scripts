using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Text;
using System.Collections;

public class ChunkSync : NetworkBehaviour
{
    [Header("Map")]
    public Map map;
    public MapSettingManager mapSettingManager;
    private Chunk[,] chunk2DArray;

    private List<byte> receivedBlockData = new List<byte>();
    private List<byte> receivedBlockHeightData = new List<byte>();

    public Dictionary<Vector2, BlockData[,,]> receivedEditChunkData = new Dictionary<Vector2, BlockData[,,]>();
    private List<byte> receivedEditBlockData = new List<byte>();

    [Header("Chunk Split")]
    [SerializeField] private int splitNumber;
    [SerializeField] private int dividingChunkNumber = 10;

    [Header("Block")]
    [SerializeField] Transform highLightBlock;
    [SerializeField] Transform placeBlock;

    [Header("Loading")]
    [SerializeField] private WorldLoading worldLoading;

    private void Awake()
	{
        splitNumber = ChunkData.ChunkHeightValue / dividingChunkNumber;
        worldLoading = GetComponent<WorldLoading>();
	}

	private void Start()
    {
        map = mapSettingManager.GetMap();
        chunk2DArray = map.GetChunksInMap();

        if (chunk2DArray == null)
            Debug.LogError("Chunk2DArray is null");
    }

    public override void OnStartServer() // 서버가 시작될 때 호출
    {
        // RegisterHandler에 NetworkMessage를 구독
        NetworkServer.RegisterHandler<ReadyMessage>(OnClientReady);

        NetworkServer.RegisterHandler<ChunkMessage>(ChunkUpdate);

        NetworkServer.RegisterHandler<EditChunkMessage>(EditChunkUpdate);

        NetworkObjectManager.Instance.RequestAuthority(gameObject);
    }

    public override void OnStartClient() // 클라이언트가 시작될 때 호출
    {
        // RegisterHandler에 NetworkMessage를 구독
        NetworkClient.RegisterHandler<ChunkMessage>(ChunkReceive);
        NetworkClient.RegisterHandler<EditChunkMessage>(EditChunkMessageReceive);
        worldLoading.StartLoading(isServer);

        if (!isClientOnly)
        {
            //mapSettingManager.DrawOcean();
            worldLoading.EndLoading(mapSettingManager, highLightBlock, placeBlock, isClientOnly);
        }
        else
        {
            NetworkClient.Send(new ReadyMessage());    
        }
    }

    #region 클라이언트 입장 시 맵 동기화
    private void OnClientReady(NetworkConnection conn, ReadyMessage msg)
    {
        Debug.Log("Client ready, Sending chunk messages");

        StartCoroutine(SendAllChunkData(conn));
    }
    
    IEnumerator SendAllChunkData(NetworkConnection conn)
    {
        for (int i = 0; i < chunk2DArray.GetLength(0); i++)
        {
            for (int j = 0; j < chunk2DArray.GetLength(1); j++)
            {
                SendChunkData(chunk2DArray[i, j], i, j, conn);
                yield return null;
                yield return null;
            }
        }
    }

    void SendChunkData(Chunk chunk, int i, int j, NetworkConnection conn)
    {
        byte[] byteBlockData = SerializeToJson(chunk.BlockInVoxel);
        byte[] byteBlockHeightData = SerializeToJson(chunk.BlocksHeightInVoxelFloor);

        int splitBlockSize = byteBlockData.Length / splitNumber;
        int splitBlockHeightSize = byteBlockHeightData.Length / splitNumber;

        for (int dataIndex = 0; dataIndex < splitNumber; dataIndex++)
        {
            bool isLastPart = dataIndex == splitNumber - 1;
            
            int blockOffset = dataIndex * splitBlockSize;
            int blockHeightOffset = dataIndex * splitBlockHeightSize;

            if (isLastPart)
            {
                // 마지막 파트에서는 남은 데이터의 크기에 맞게 size를 조정한다.
                splitBlockSize += byteBlockData.Length % splitNumber;
                splitBlockHeightSize += byteBlockHeightData.Length % splitNumber;
            }

            byte[] splitBlockData = new byte[splitBlockSize];
            byte[] splitBlockHeight = new byte[splitBlockHeightSize];

            // Array.Copy(복사할 배열, 복사를 시작할 위치, 복사될 배열, 복사되는 위치, 복사할 길이);
            Array.Copy(byteBlockData, blockOffset, splitBlockData, 0, splitBlockSize);
            Array.Copy(byteBlockHeightData, blockHeightOffset, splitBlockHeight, 0, splitBlockHeightSize);

            ChunkMessage chunkMessage = new ChunkMessage
            {
                blockInVoxel = splitBlockData,
                blocksHeightInVoxelFloor = splitBlockHeight,

                isLastPart = isLastPart,
                x = i,
                y = j,
                chunkLoadingPercent = (i * chunk2DArray.GetLength(1) + j + 1) / (float)chunk2DArray.Length,
            };

            conn.Send(chunkMessage);
        }
    }

    // ChunkMessage를 수신했을 때 호출
    private void ChunkReceive(ChunkMessage message)
    {
        if (message.blockInVoxel != null) // List에 나눠져서 온 데이터 저장
            receivedBlockData.AddRange(message.blockInVoxel);

        if (message.blocksHeightInVoxelFloor != null) // List에 나눠져서 온 데이터 저장
            receivedBlockHeightData.AddRange(message.blocksHeightInVoxelFloor);

        if (message.isLastPart) // 한 청크의 마지막 파트라면
        {
            // 수신받은 데이터를 역직렬화한다.
            BlockData[,,] blockInVoxel = 
                DeserializeFromJson<BlockData[,,]>(receivedBlockData.ToArray());

            BlockHeightData[,] blocksHeightInVoxelFloor = 
                DeserializeFromJson<BlockHeightData[,]>(receivedBlockHeightData.ToArray());

            // 역직렬화한 데이터를 청크에 로드시킨다.
            chunk2DArray[message.x, message.y]
                .LoadChunk(blockInVoxel, blocksHeightInVoxelFloor);

            receivedBlockData.Clear();
            receivedBlockHeightData.Clear();

            // 로딩바 업데이트
            worldLoading.LoadingBarControl(message.chunkLoadingPercent);

            if (message.chunkLoadingPercent == 1f)
            {
                // 청크 로딩이 완료되면 후처리 작업을 해준다.
                UpdateVisuals();
            }
        }
    }

    private void UpdateVisuals()
    {
        map.SetUpChunks(chunk2DArray);

        map.ReDrawMap();

        worldLoading.EndLoading(mapSettingManager, highLightBlock, placeBlock, isClientOnly);
    }

    private void ChunkUpdate(NetworkConnection sender, ChunkMessage message)
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn != sender) // 서버에 이 메시지를 송신한 클라이언트가 아닐 때
            {
                conn.Send(message);
            }
        }
    }
    #endregion

    #region Json 직렬화 및 역직렬화
    /// <summary>
    /// Json 직렬화 메서드
    /// </summary>
    /// <typeparam name="T">자료형(입력 안해도 됨)</typeparam>
    /// <param name="data">byte[]로 직렬화할 매개변수</param>
    public byte[] SerializeToJson<T>(T data)
    {
        string json = JsonConvert.SerializeObject(data, Formatting.None, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Json 역직렬화 메서드
    /// </summary>
    /// <typeparam name="T">자료형</typeparam>
    /// <param name="data">역직렬화할 byte[]</param>
    public T DeserializeFromJson<T>(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json);
    }
    #endregion

    #region 하나의 청크만 동기화
    public void EditChunk(Vector2 vector2, Chunk chunk)
    {
        byte[] byteBlockData = SerializeToJson(chunk.BlockInVoxel);

        int splitBlockSize = byteBlockData.Length / splitNumber;

        for (int dataIndex = 0; dataIndex < splitNumber; dataIndex++)
        {
            bool isLastPart = dataIndex == splitNumber - 1;

            int blockOffset = dataIndex * splitBlockSize;

            if (isLastPart)
            {
                splitBlockSize += byteBlockData.Length % splitNumber;
            }

            byte[] splitBlockData = new byte[splitBlockSize];

            Array.Copy(byteBlockData, blockOffset, splitBlockData, 0, splitBlockSize);

            EditChunkMessage editChunkMessage = new EditChunkMessage
            {
                blockInVoxel = splitBlockData,
                isLastPart = isLastPart,
                vec2 = vector2
            };
            
            NetworkClient.Send(editChunkMessage);
        }

    }

    private void EditChunkUpdate(NetworkConnection sender, EditChunkMessage message)
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn != sender && conn.isReady)
            {
                conn.Send(message);
            }
        }
    }

    private void EditChunkMessageReceive(EditChunkMessage message)
    {
        if (message.blockInVoxel != null) receivedEditBlockData.AddRange(message.blockInVoxel);

        if (message.isLastPart)
        {   
            BlockData[,,] blockInVoxel = DeserializeFromJson<BlockData[,,]>(receivedEditBlockData.ToArray());

            if(LopNetworkManager.isLoading)
            {
                if(receivedEditChunkData.ContainsKey(message.vec2))
                {
                    receivedEditChunkData[message.vec2] = blockInVoxel;
                }
                else
                {
                    receivedEditChunkData.Add(message.vec2, blockInVoxel);
                }
            }
            else
            {
                Chunk chunk = chunk2DArray[(int)message.vec2.x, (int)message.vec2.y];
                chunk.LoadChunk(blockInVoxel, null);
                chunk.AssignBlocksToMap();
                chunk.UpdateChunk();
            }

            receivedEditBlockData.Clear();
        }
    }

    public void SetEditChunk()
    {
        if (receivedEditChunkData.Count == 0) return;

        foreach (var editChunkData in receivedEditChunkData)
        {
            Chunk chunk = chunk2DArray[(int)editChunkData.Key.x, (int)editChunkData.Key.y];
            chunk.LoadChunk(editChunkData.Value, null);
            chunk.AssignBlocksToMap();
            chunk.UpdateChunk();
        }
    }
    #endregion

    public void SurroundingChunk(Vector2 vector2, Chunk chunk)
    {
        StartCoroutine(Co_SurroundingChunk(vector2, chunk));
    }

    IEnumerator Co_SurroundingChunk(Vector2 vector2, Chunk chunk)
    {
        yield return null;

        EditChunk(vector2, chunk);
    }

    [System.Serializable]
    public struct ChunkMessage : NetworkMessage
    {
        public byte[] blockInVoxel; // BlockData[,,]
        public byte[] blocksHeightInVoxelFloor; // BlockHeightData[,]
        
        public bool isLastPart;         // 한 청크의 마지막 데이터인지 아닌지 확인
        public int x;                   // Chunk 2차원 배열의 x
        public int y;                   // Chunk 2차원 배열의 y 
        public float chunkLoadingPercent;        // 청크들을 로딩한 비율
    }

    [System.Serializable]
    public struct EditChunkMessage : NetworkMessage
    {
        public byte[] blockInVoxel; // BlockData[,,]

        public bool isLastPart;
        public Vector2 vec2;
    }

    // 클라이언트가 서버에서 메시지를 받을 준비가 됐음을 알리는 메시지, 서버는 이 메시지를 받아서 맵의 데이터를 건네줘야 할 클라이언트를 알아낸다.
    public struct ReadyMessage : NetworkMessage { }

    #region 날마다 사라지는 블록 동기화
    private List<byte> receivedBlockInfo = new List<byte>();
    private const int dataChunkSize = 290000;

    public void SyncRemoveBlocks(List<BlockInfo> blockInfoList)
    {
        StartCoroutine(Co_SyncRemoveBlocks(blockInfoList));
    }

    private IEnumerator Co_SyncRemoveBlocks(List<BlockInfo> blockInfoList)
    {
        byte[] fullData = SerializeToJson(blockInfoList.ToArray());
        int dataChunkNum = Mathf.CeilToInt((float)fullData.Length / dataChunkSize); // 올림을 위함

        for (int i = 0; i < dataChunkNum; i++)
        {
            int currentDataChunkSize = Mathf.Min(dataChunkSize, fullData.Length - i * dataChunkSize);
            byte[] dataChunk = new byte[currentDataChunkSize];
            Array.Copy(fullData, i * dataChunkSize, dataChunk, 0, currentDataChunkSize);

            if (isServerOnly)
                RpcSyncRemoveBlocks(dataChunk, i, dataChunkNum);
            else
                CmdSyncRemoveBlocks(dataChunk, i, dataChunkNum);

            yield return null;
        }
    }

    [Command]
    private void CmdSyncRemoveBlocks(byte[] blockInfosToByte, int currentDataChunkNum, int totalDataChunkNum)
    {
        RpcSyncRemoveBlocks(blockInfosToByte, currentDataChunkNum, totalDataChunkNum);
    }

    [ClientRpc]
    private void RpcSyncRemoveBlocks(byte[] blockInfosToByte, int currentDataChunkNum, int totalDataChunkNum)
    {
        if (isServer) { return; }

        // 조각을 리스트에 추가
        receivedBlockInfo.AddRange(blockInfosToByte);

        if(LopNetworkManager.isLoading) { return; }

        // 모든 조각을 받았는지 확인
        if (currentDataChunkNum == totalDataChunkNum - 1)
        {
            // 원래 데이터로 역직렬화하고 map.SyncRemoveBlocks 호출
            BlockInfo[] blockInfos = DeserializeFromJson<BlockInfo[]>(receivedBlockInfo.ToArray());
            map.SyncRemoveBlocks(blockInfos);

            // 리스트 초기화
            receivedBlockInfo.Clear();
        }
    }

    public void SetRemoveBlocks()
    {
        if (receivedBlockInfo.Count == 0) return;

        BlockInfo[] blockInfos = DeserializeFromJson<BlockInfo[]>(receivedBlockInfo.ToArray());
        map.SyncRemoveBlocks(blockInfos);

        receivedBlockInfo.Clear();
    }
    #endregion
}