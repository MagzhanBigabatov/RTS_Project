using UnityEngine;

public class CommandPoint : MonoBehaviour
{
    [SerializeField] private Transform commandPosition;

    public Vector3 WorldPosition
    {
        get
        {
            return commandPosition != null
                ? commandPosition.position
                : transform.position;
        }
    }
}