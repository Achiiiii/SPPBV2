using System;
using UnityEngine;

public static class ExponentialSmoothing
{
	public static float filter(float state, float data, float alpha) => alpha * data + (1 - alpha) * state;
	public static double filter(double state, double data, double alpha) => alpha * data + (1 - alpha) * state;
	public static Vector2 filter(Vector2 state, Vector2 data, float alpha) => alpha * data + (1 - alpha) * state;
}

public class Timer
{
	private DateTimeOffset last;
	public Timer() => lap();
	public double lap()
	{
		var now = DateTimeOffset.Now;
		var diff = now - last;
		last = now;
		return diff.TotalSeconds;
	}
}

public class Counter
{
	private double alpha = 0.5;
	private double dec = 0.99f;
	private Timer timer = new Timer();
	public double freq{get;private set;} = 0;
	public int count{get;private set;} = 0;

	public Counter(double alpha = 0.5, double dec = 0.99)
	{
		this.alpha = alpha;
		this.dec = dec;
	}
	public void Update(bool data)
	{
		if(data)
		{
			count++;
			var lap = 1/timer.lap();
			freq = ExponentialSmoothing.filter(freq, lap, alpha);
		}
		else
		{
			freq *= dec;
		}
	}
}