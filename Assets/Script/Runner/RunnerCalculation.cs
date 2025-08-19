using System.Collections;
using UnityEngine;

public class RunnerCalculation : MonoBehaviour
{
    [SerializeField] private Transform _runnerTransform;
    private WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
    /// <summary>
    /// ランナーの走塁計算
    /// </summary>
    /// <param name="arrivaltime">次の塁に到達するまでの時間</param>
    /// <param name="startPoint">どれくらいリードしているか（バッターはゼロ）</param>
    /// <returns></returns>
    public IEnumerator RunningCalculate(float arrivaltime, float startPoint, Transform nextBase, Transform currentBase)
    {
        float t = startPoint;
        while (t < 1)
        {
            //Debug.Log(t);
            t += Time.deltaTime / arrivaltime;
            _runnerTransform.position = Vector3.Lerp(nextBase.position, currentBase.position, t);
            yield return _waitForEndOfFrame;
        }
    }
}
