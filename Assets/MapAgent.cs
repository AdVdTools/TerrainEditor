using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapAgent : MonoBehaviour {

    [SerializeField] private float distanceToFeet = 1f;

    [SerializeField] private Map map;

    private Transform camTransform;

    private Vector3 velocity;
    private Vector3 targetDirection;
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float accelSmoothTime = 1f;

    
    void Awake () {
        Input.simulateMouseWithTouches = false;
        //TODO move input somewhere else?
        Camera mainCam = Camera.main;
        if (mainCam != null) camTransform = mainCam.transform;

        if (map != null) map.POVTransform = this.transform;
    }
	
	// Update is called once per frame
	void Update () {
		if (map != null && map.Data != null)
        {
            Vector3 localPosition = map.transform.worldToLocalMatrix.MultiplyPoint3x4(transform.position);
            Vector3 mapUp = map.transform.up;

            float terrainHeight = map.Data.SampleHeight(localPosition.x, localPosition.z);

            float elevation = localPosition.y - distanceToFeet - terrainHeight;
            //Debug.Log(elevation + " " + mapUp);
            transform.position -= mapUp * elevation;

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Vector2 screenDirection;
            if (Input.touchCount > 0) {
                Touch touch = Input.GetTouch(0);
                screenDirection = touch.position - screenCenter;
            }
            else if (Input.GetMouseButton(0))
            {
                screenDirection = (Vector2)Input.mousePosition - screenCenter;
            }
            else
            {
                screenDirection = Vector2.zero;
            }

            if (camTransform != null)
            {
                Vector3 camDirection = camTransform.right * screenDirection.x + camTransform.up * screenDirection.y;
                Vector3 mapDirection = Vector3.ProjectOnPlane(camDirection, mapUp).normalized;

                targetDirection = mapDirection;
            }
        }
    }

    private void FixedUpdate()
    {
        velocity += (targetDirection * maxSpeed - velocity) * (Time.fixedDeltaTime / accelSmoothTime);

        transform.position += velocity * Time.fixedDeltaTime;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 down = map != null ? -map.transform.up : -Vector3.up;

        Gizmos.DrawRay(transform.position, distanceToFeet * down);
    }
}
