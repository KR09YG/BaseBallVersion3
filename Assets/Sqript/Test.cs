using UnityEngine;

public class Test : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.AddForce(new Vector3(12, 0, -27),ForceMode.Impulse);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
