using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Lop.Survivor.Event.Mission;
using Mirror;
using System.Collections.Generic;
using System;

public class MissionManager : NetworkBehaviour
{
    public static MissionManager Instance { get; set; }

    [Header("Scriptable Object")]
    [SerializeField] private MissionDataScriptable missionDatas;

    [Header("Mission Data")]
    public string       nameText;
    public string       descText;
    [SyncVar(hook = nameof(HookMissionOrder))]
    public int          missionOrder = 1;
    public MissionType  missionType;
    public string       goalTarget;
    public int          goalValue;

    [Header("Mission UI")]
    [SerializeField] private Image      missionBox;
    [SerializeField] private TMP_Text   nameTMP;
    [SerializeField] private TMP_Text   descTMP;
    [SerializeField] private TMP_Text   stageOfCompletionTMP;
    [SerializeField] private GameObject checkmark;

    private WaitForSeconds waitForMissionUIChange = new WaitForSeconds(1f);

    [Header("Mission Event")]
    // MissionType으로 어떤 분류의 미션인지 확인, string은 target의 이름, int는 미션을 클리어하기 위한 행위를 해야 하는 횟수
    public Dictionary<MissionType, Dictionary<string, int>> eventDictionary = new Dictionary<MissionType, Dictionary<string, int>>();

    [SerializeField] private bool isMissionChange;
    private const int missionAmount = 41;
    [SerializeField] private FindMission findMission;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void EventDataLoad()
    {
        foreach(MissionType type in Enum.GetValues(typeof(MissionType)))
        {
            eventDictionary.Add(type, new Dictionary<string, int>());
        }

        foreach(var data in missionDatas.missionDatas)
        {
            if(eventDictionary[data.missionType].ContainsKey(data.nameText))
            {
                eventDictionary[data.missionType].Add(data.nameText, 0);
            }
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        NetworkServer.RegisterHandler<SetStageOfCompletionMessage>(ReceiveSetStageOfCompletionMessage);
        NetworkServer.RegisterHandler<RemoveCollectEventMessage>(ReceiveRemoveCollectEventMessage);
        NetworkServer.RegisterHandler<EventCollectMessage>(ReceiveEventCollectMessage);
        EventDataLoad();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        NetworkClient.RegisterHandler<SetStageOfCompletionMessage>(ReceiveSetStageOfCompletionMessage);
    }

    public void StartMission()
    {
        findMission = LopNetworkManager.GetPlayer().GetComponentInChildren<FindMission>();

        MissionDataLoad();
    }

    private void MissionDataLoad()
    {
        MissionData missionData = missionDatas.missionDatas[missionOrder];

        nameText = missionData.nameText;
        descText = missionData.descText;
        missionType = missionData.missionType;
        goalTarget = missionData.goalTarget;
        goalValue = missionData.goalValue;


        if (isServer)
            SetStageOfCompletion();

        if(findMission != null)
            findMission.MissionCheck(missionType, goalTarget); // 건축물 관련 미션일 시 건축물을 화살표로 가리킴.

        nameTMP.text = nameText;
        descTMP.text = descText;
        checkmark.SetActive(false);
    }

    #region 미션 진행도
    public void SetMissionUI()
    {
        NetworkClient.Send(new SetStageOfCompletionMessage());
    }

    public struct SetStageOfCompletionMessage : NetworkMessage 
    {
        public string stageOfCompletionText;
    }

    private void ReceiveSetStageOfCompletionMessage(NetworkConnection conn, SetStageOfCompletionMessage msg) // 서버에서 받았을 때
    {
        msg.stageOfCompletionText = stageOfCompletionTMP.text;
        conn.Send(msg);
    }

    private void ReceiveSetStageOfCompletionMessage(SetStageOfCompletionMessage msg) // 클라이언트에서 받았을 때
    {
        stageOfCompletionTMP.text = msg.stageOfCompletionText;
    }

    private void SetStageOfCompletion()
    {
        stageOfCompletionTMP.text = $"{eventDictionary[missionType][goalTarget]} / {goalValue}";

        RpcSetStageOfCompletion(stageOfCompletionTMP.text);
    }

    [ClientRpc]
    private void RpcSetStageOfCompletion(string stageOfCompletionText)
    {
        if(isClientOnly)
            stageOfCompletionTMP.text = stageOfCompletionText;
    }
    #endregion

    #region 수집된 수집 이벤트 줄이기
    public void RemoveCollectEvent(string target, int discount)
    {
        if (eventDictionary[MissionType.Collect].ContainsKey(target))
        {
            if(isServerOnly)
            {
                eventDictionary[MissionType.Collect][target] -= discount;

                if (eventDictionary[MissionType.Collect][target] < 0) eventDictionary[MissionType.Collect][target] = 0;

                if (missionType == MissionType.Collect && target == goalTarget)
                {
                    SetStageOfCompletion();
                }
            }
            else
            {
                NetworkClient.Send(new RemoveCollectEventMessage
                {
                    target = target,
                    discount = discount
                });
            }
        }
    }

    private void ReceiveRemoveCollectEventMessage(NetworkConnection conn, RemoveCollectEventMessage msg)
    {
        eventDictionary[MissionType.Collect][msg.target] -= msg.discount;

        if (eventDictionary[MissionType.Collect][msg.target] < 0) eventDictionary[MissionType.Collect][msg.target] = 0;

        if (missionType == MissionType.Collect && msg.target == goalTarget)
        {
            SetStageOfCompletion();
        }
    }

    public struct RemoveCollectEventMessage : NetworkMessage
    {
        public string target;
        public int discount;
    }
    #endregion

   #region 이벤트 수집
    /// <summary>
    /// 미션에 맞는 이벤트를 수집
    /// </summary>
    /// <param name="type">미션 종류</param>
    /// <param name="target">수집할 이벤트의 목표 대상</param>
    public void EventCollect(MissionType type, string target)
    {
        if (LopNetworkManager.isLoading) { return; }

        if (isServer)
        {
            UpdateEventCollectState(type, target);
        }
        else
        {
            NetworkClient.Send(new EventCollectMessage
            {
                type = type,
                target = target
            });
        }
    }

    /// <summary>
    /// 이벤트 수집 상태 업데이트
    /// </summary>
    /// <param name="type"></param>
    /// <param name="target"></param>
    private void UpdateEventCollectState(MissionType type, string target)
    {
        if(eventDictionary[type].ContainsKey(target))
            {
                eventDictionary[type][target]++;

                if(target == goalTarget && missionType == type)
                {     
                    SetStageOfCompletion();
                    
                    if(eventDictionary[type][target] >= goalValue)
                    {
                        IncreaseMissionOrder();
                    }        
                }
            }
    }

    private void ReceiveEventCollectMessage(NetworkConnection conn, EventCollectMessage msg)
    {
        UpdateEventCollectState(msg.type, msg.target);
    }

    public struct EventCollectMessage : NetworkMessage
    {
        public MissionType type;
        public string target;
    }
    #endregion

    private void IncreaseMissionOrder()
    {
        if (!isMissionChange && missionAmount > missionOrder)
        {
            eventDictionary[MissionType.Special]["Mission"]++;
            missionOrder++;
        }
        else if (missionAmount == missionOrder)
        {
            checkmark.SetActive(true);
            checkmark.GetComponent<Animator>().SetTrigger("Mission");
        }
    }

    #region 미션 클리어
    // 이미 미션 클리어 조건을 충족했는지 확인
    public void ImmediatelyCheckMissionClear()
    {
        if (!isServer) { return; }

        if (eventDictionary[missionType].ContainsKey(goalTarget) && eventDictionary[missionType][goalTarget] >= goalValue)
        {
            IncreaseMissionOrder();
        }
    }

    private void HookMissionOrder(int oldMissionId, int newMissionId)
    {
        StartCoroutine(Co_MissionClear());
    }

    private IEnumerator Co_MissionClear()
    {
        isMissionChange = true;
        checkmark.SetActive(true);
        checkmark.GetComponent<Animator>().SetTrigger("Mission");
        SoundManager.Instance.PlaySFX("NextDay"); 

        descTMP.fontStyle = FontStyles.Strikethrough;

        yield return waitForMissionUIChange;

        float elapsedTime = 0f;
        float duration = 1f;

        Vector2 showPos = missionBox.rectTransform.anchoredPosition;
        Vector2 hidePos = missionBox.rectTransform.anchoredPosition;
        hidePos.x -= 450f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            missionBox.rectTransform.anchoredPosition = Vector2.Lerp(showPos, hidePos, t);
            yield return null;
        }

        MissionDataLoad();
        yield return waitForMissionUIChange;

        elapsedTime = 0f;
        descTMP.fontStyle = FontStyles.Normal;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            missionBox.rectTransform.anchoredPosition = Vector2.Lerp(hidePos, showPos, t);
            yield return null;
        }
        isMissionChange = false;

        ImmediatelyCheckMissionClear();
    }
    #endregion


}