using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Lop.Survivor.Event.Mission;
using Mirror;
using System.Collections.Generic;

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
    [SerializeField] private List<string> collectList;
    [SerializeField] private List<string> makeList;
    [SerializeField] private List<string> installationVitalizeList;
    [SerializeField] private List<string> specialList;

    public Dictionary<string, int> collectEvents                = new Dictionary<string, int>();
    public Dictionary<string, int> makeEvents                   = new Dictionary<string, int>();
    public Dictionary<string, int> installationVitalizeEvents   = new Dictionary<string, int>();
    public Dictionary<string, int> deliverEvents                = new Dictionary<string, int>();
    public Dictionary<string, int> specialEvents                = new Dictionary<string, int>();

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
        foreach (string key in collectList)
        {
            collectEvents.Add(key, 0);
        }
        foreach (string key in makeList)
        {
            makeEvents.Add(key, 0);
        }
        foreach (string key in installationVitalizeList)
        {
            installationVitalizeEvents.Add(key, 0);
            deliverEvents.Add(key, 0);
        }
        foreach (string key in specialList)
        {
            specialEvents.Add(key, 0);
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
        foreach (var missionData in missionDatas.missionDatas)
        {
            if (missionOrder == missionData.order)
            {
                nameText = missionData.nameText;
                descText = missionData.descText;
                missionType = missionData.missionType;
                goalTarget = missionData.goalTarget;
                goalValue = missionData.goalValue;

            }
        }

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
        switch (missionType)
        {
            case MissionType.Collect:
                stageOfCompletionTMP.text = $"{collectEvents[goalTarget]} / {goalValue}";
                break;
            case MissionType.Make:
                stageOfCompletionTMP.text = $"{makeEvents[goalTarget]} / {goalValue}";
                break;
            case MissionType.InstallationVitalize:
                stageOfCompletionTMP.text = $"{installationVitalizeEvents[goalTarget]} / {goalValue}";
                break;
            case MissionType.Deliver:
                stageOfCompletionTMP.text = $"{deliverEvents[goalTarget]} / {goalValue}";
                break;
            case MissionType.Special:
                stageOfCompletionTMP.text = $"{specialEvents[goalTarget]} / {goalValue}";
                break;
        }

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
        if (collectEvents.ContainsKey(target))
        {
            if(isServerOnly)
            {
                collectEvents[target] -= discount;

                if (collectEvents[target] < 0) collectEvents[target] = 0;

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
        collectEvents[msg.target] -= msg.discount;

        if (collectEvents[msg.target] < 0) collectEvents[msg.target] = 0;

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
        if (isServer)
        {
            switch (type)
            {
                case MissionType.Collect:

                    if (collectEvents.ContainsKey(target))
                    {
                        if (++collectEvents[target] >= goalValue && target == goalTarget && missionType == type)
                            IncreaseMissionOrder();
                    }
                    break;
                case MissionType.Make:
                    if (makeEvents.ContainsKey(target))
                    {
                        if (++makeEvents[target] >= goalValue && target == goalTarget && missionType == type)
                            IncreaseMissionOrder();
                    }
                    break;
                case MissionType.InstallationVitalize:
                    if (installationVitalizeEvents.ContainsKey(target))
                    {
                        if (++installationVitalizeEvents[target] >= goalValue && target == goalTarget && missionType == type)
                            IncreaseMissionOrder();
                    }
                    break;
                case MissionType.Deliver:
                    if (deliverEvents.ContainsKey(target))
                    {
                        if (++deliverEvents[target] >= goalValue && target == goalTarget && missionType == type)
                            IncreaseMissionOrder();
                    }
                    break;
                case MissionType.Special:
                    if (specialEvents.ContainsKey(target))
                    {
                        if (++specialEvents[target] >= goalValue && target == goalTarget && missionType == type)
                            IncreaseMissionOrder();
                    }
                    break;
            }

            if (missionType == type && goalTarget == target)
            {
                SetStageOfCompletion();
            }
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

    private void ReceiveEventCollectMessage(NetworkConnection conn, EventCollectMessage msg)
    {
        switch (msg.type)
        {
            case MissionType.Collect:

                if (collectEvents.ContainsKey(msg.target))
                {
                    if (++collectEvents[msg.target] >= goalValue && msg.target == goalTarget && missionType == msg.type)
                        IncreaseMissionOrder();
                }
                break;
            case MissionType.Make:
                if (makeEvents.ContainsKey(msg.target))
                {
                    if (++makeEvents[msg.target] >= goalValue && msg.target == goalTarget && missionType == msg.type)
                        IncreaseMissionOrder();
                }
                break;
            case MissionType.InstallationVitalize:
                if (installationVitalizeEvents.ContainsKey(msg.target))
                {
                    if (++installationVitalizeEvents[msg.target] >= goalValue && msg.target == goalTarget && missionType == msg.type)
                        IncreaseMissionOrder();
                }
                break;
            case MissionType.Deliver:
                if (deliverEvents.ContainsKey(msg.target))
                {
                    if (++deliverEvents[msg.target] >= goalValue && msg.target == goalTarget && missionType == msg.type)
                        IncreaseMissionOrder();
                }
                break;
            case MissionType.Special:
                if (specialEvents.ContainsKey(msg.target))
                {
                    if (++specialEvents[msg.target] >= goalValue && msg.target == goalTarget && missionType == msg.type)
                        IncreaseMissionOrder();
                }
                break;
        }

        if (missionType == msg.type && goalTarget == msg.target)
        {
            SetStageOfCompletion();
        }
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
            specialEvents["Mission"]++;
            missionOrder++;
        }
        else if (missionAmount == missionOrder)
        {
            checkmark.SetActive(true);
            checkmark.GetComponent<Animator>().SetTrigger("Mission");
        }
    }

    #region 미션 클리어
    public void ImmediatelyCheckMissionClear()
    {
        if (!isServer) { return; }

        switch (missionType)
        {
            case MissionType.Collect:

                if (collectEvents.ContainsKey(goalTarget) && collectEvents[goalTarget] >= goalValue)
                {
                    IncreaseMissionOrder();
                }
                break;
            case MissionType.Make:
                if (makeEvents.ContainsKey(goalTarget) && makeEvents[goalTarget] >= goalValue)
                {
                    IncreaseMissionOrder();
                }
                break;
            case MissionType.InstallationVitalize:
                if (installationVitalizeEvents.ContainsKey(goalTarget) && installationVitalizeEvents[goalTarget] >= goalValue)
                {
                    IncreaseMissionOrder();
                }
                break;
            case MissionType.Deliver:
                if (deliverEvents.ContainsKey(goalTarget) && deliverEvents[goalTarget] >= goalValue)
                {
                    IncreaseMissionOrder();
                }
                break;
            case MissionType.Special:
                if (specialEvents.ContainsKey(goalTarget) && specialEvents[goalTarget] >= goalValue)
                {
                    IncreaseMissionOrder();
                }
                break;
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