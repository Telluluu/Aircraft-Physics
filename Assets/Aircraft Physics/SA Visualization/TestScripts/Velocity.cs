using UnityEngine;

public class Velocity : MonoBehaviour
{
    public float velocity = 200.0f;
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    private void Update()
    {
        rb.linearVelocity = new Vector3(velocity / 3, 0, velocity / 3);
    }
}
