namespace Lop.Survivor.Event.Mission
{
    public enum MissionType
    {
        /// <summary>
        /// 채집
        /// </summary>
        Collect,

        /// <summary>
        /// 제작
        /// </summary>
        Make,

        /// <summary>
        /// 장치 활성화
        /// </summary>
        InstallationVitalize,

        /// <summary>
        /// 납품
        /// </summary>
        Deliver,

        /// <summary>
        /// 특수
        /// </summary>
        Special
    }

    [System.Serializable]
    public class MissionData
    {   
        public string nameText;
        public string descText;
        public int order;
        public MissionType missionType;
        public string goalTarget;
        public int goalValue;
    }
}
