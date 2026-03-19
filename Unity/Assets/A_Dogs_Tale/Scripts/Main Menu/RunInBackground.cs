using UnityEngine;

public class RunInBackground : MonoBehaviour
{
    public bool runInBackground = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    void Awake()
    {
        Application.runInBackground = this.runInBackground; 
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
