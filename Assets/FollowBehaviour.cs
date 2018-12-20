using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowBehaviour : MonoBehaviour {

    [SerializeField] private Transform target;
    [SerializeField] private float camHeight = 4f;
    [SerializeField] private float camDistance = 10f;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		if (target != null)
        {
            Vector3 position = transform.position;
            Vector3 targetPosition = target.transform.position;
            Vector3 offset = position - targetPosition;
            Vector3 targetOffset = offset;
            
            float distance = Mathf.Sqrt(targetOffset.x * targetOffset.x + targetOffset.z * targetOffset.z);
            //Debug.Log(distance + " " + targetOffset);
            targetOffset *= camDistance / distance;
            targetOffset.y = camHeight;

            //Debug.Log(targetPosition + " " + targetOffset);
            transform.position = targetPosition + targetOffset;

            transform.LookAt(target);
        }
	}
}
