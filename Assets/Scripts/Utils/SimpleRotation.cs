using UnityEngine;

public class SimpleRotation : MonoBehaviour {
    [SerializeField] private int rotationAngle;

    private void Update() {
        transform.Rotate(rotationAngle, rotationAngle, rotationAngle, Space.Self);
    }
}
