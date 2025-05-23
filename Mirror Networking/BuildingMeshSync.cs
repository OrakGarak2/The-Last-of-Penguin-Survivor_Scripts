using UnityEngine;
using Lop.Survivor.Island.Buildingbase;

using Mirror;
using UnityEngine.UI;

public class BuildingMeshSync : NetworkBehaviour
{
    #region 빌딩 메쉬 동기화
    [SyncVar(hook = nameof(OnChildRotationChanged))] public float meshRotY;
    [SyncVar(hook = nameof(OnChildPositionChanged))] public float meshPosY;

    private void OnChildRotationChanged(float oldRotY, float newRotY)
    {
        NetworkIdentity identity = GetComponent<NetworkIdentity>();

        if (identity != null)
        {
            // 자식 오브젝트의 Y축 회전 값만 변경
            Transform MeshTransform = transform.GetChild(0).transform;

            MeshTransform.rotation = Quaternion.Euler(
                MeshTransform.rotation.eulerAngles.x,
                newRotY,
                MeshTransform.rotation.eulerAngles.z
            );
        }
    }

    private void OnChildPositionChanged(float oldPosY, float newPosY)
    {
        NetworkIdentity identity = GetComponent<NetworkIdentity>();

        if (identity != null)
        {
            // 자식 오브젝트의 Y축 회전 값만 변경
            Transform MeshTransform = transform.GetChild(0).transform;

            MeshTransform.position = new Vector3(MeshTransform.position.x, newPosY, MeshTransform.position.z);
        }
    }
    #endregion
}
