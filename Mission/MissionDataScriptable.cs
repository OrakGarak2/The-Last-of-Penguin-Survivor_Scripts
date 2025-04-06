using UnityEngine;
using Lop.Survivor.Event.Mission;

[CreateAssetMenu(fileName = "Mission Datas", menuName = "Data/Mission")]
public class MissionDataScriptable : ScriptableObject
{
    public MissionData[] missionDatas; 
}
