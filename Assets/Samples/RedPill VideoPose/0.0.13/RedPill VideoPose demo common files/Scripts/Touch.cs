using System;
using UnityEngine;

public class Touch : MonoBehaviour
{
	private int maxCount = 0;
	public event Action tapped;
	void Update()
	{
		if(maxCount == 5 && Input.touchCount == 0)
		{
			maxCount = 0;
			tapped?.Invoke();
		}
		else
			maxCount = Math.Max(Input.touchCount, maxCount);
	}
}
