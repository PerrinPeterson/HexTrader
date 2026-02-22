using UnityEngine;

public class playerController : MonoBehaviour
{

    public Vector2Int playMatSize = new Vector2Int(5, 5);
    public Vector2Int startPoint = new Vector2Int(10, 10);
    public GameObject gridTilePool;
    public GameObject worldManagerObject;
    public float moveSpeed = 5.0f;
    public float moveSpeedIncrement = 5.0f;
    public float minMoveSpeed = 5.0f;
    public float maxMoveSpeed = 50.0f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gridTilePool.GetComponent<TilePool>().player = this;
        gridTilePool.GetComponent<TilePool>().worldManager = GameObject.Find("WorldManager").GetComponent<WorldManager>();
        gridTilePool.GetComponent<TilePool>().GeneratePool(playMatSize.x * playMatSize.y);
        worldManagerObject.GetComponent<WorldManager>().Init();

        gridTilePool.GetComponent<TilePool>().PlaceAroundCenter(new Vector2(transform.position.x, transform.position.z), playMatSize, startPoint);
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 direction = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        if (Input.GetKeyDown(KeyCode.Q))
        {
            ChangeScrollSpeed(-moveSpeedIncrement);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            ChangeScrollSpeed(moveSpeedIncrement);
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                worldManagerObject.GetComponent<WorldManager>().Regen();
                gridTilePool.GetComponent<TilePool>().PlaceAroundCenter(new Vector2(transform.position.x, transform.position.z), playMatSize, startPoint);
            }
        }

        gridTilePool.GetComponent<TilePool>().MovePlaymat(direction, Time.deltaTime, moveSpeed);

        //Scrollwheel zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Zoom(scroll);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitGame();
        }
    }
    private void FixedUpdate()
    {
    }

    void ChangeScrollSpeed(float increment)
    {
        moveSpeed += increment;
        if (moveSpeed < minMoveSpeed)
        {
            moveSpeed = minMoveSpeed;
        }
        else if (moveSpeed > maxMoveSpeed)
        {
            moveSpeed = maxMoveSpeed;
        }
    }

    void Zoom(float value)
    {
        Camera.main.transform.position += Camera.main.transform.forward * value * 10.0f;
        if (Camera.main.transform.position.y < 5.0f)
        {
            Camera.main.transform.position = new Vector3(Camera.main.transform.position.x, 5.0f, Camera.main.transform.position.z);
        }
        else if (Camera.main.transform.position.y > 35.0f)
        {
            Camera.main.transform.position = new Vector3(Camera.main.transform.position.x, 35.0f, Camera.main.transform.position.z);
        }
    }

    void ExitGame()
    {
        Application.Quit();
    }
}
