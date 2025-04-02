namespace Browser.JS;

public class JsThread
{
    private Thread _thread;
    private string _fileName;
    public int Number;
    public string Content { get; set; }

    public JsThread(int number, string fileName)
    {
        Number = number;
        _fileName = fileName;
        _thread = new Thread(new ThreadStart(this.RunThread));
    }

    // Thread methods / properties
    public void Start() => _thread.Start();
    public void Join() => _thread.Join();
    public bool IsAlive => _thread.IsAlive;

    // Override in base class
    private void RunThread()
    {
        Content = File.ReadAllText(_fileName);
    }
}