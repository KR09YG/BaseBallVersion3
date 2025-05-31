using UnityEngine;

public class IsHomeRun : MonoBehaviour
{
    [SerializeField] Animator _homerunTextAnim;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _homerunTextAnim.Play("HomeRunTextAnim");
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(other.tag);
        if (other.gameObject.CompareTag("Ball"))
        {
            _homerunTextAnim.Play("HomeRunTextAnim");
        }
    }
}
