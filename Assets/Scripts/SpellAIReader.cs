using Unity.InferenceEngine;
using UnityEngine;

public class SpellAIReader : MonoBehaviour
{
    [Header("Sentis Configuration")]
    [SerializeField] private ModelAsset modelAsset; // File .onnx

    // EXACT ORDER OF LABELS AS THEY APPEAR IN TEACHABLE MACHINE
    [SerializeField] private string[] labels = { "Fire Sigil", "Column", "Garbage" };

    private Model runtimeModel;
    private Worker worker;

    void Awake()
    {
        if (modelAsset != null)
        {
            // Load the ONNX asset into Sentis runtime format
            runtimeModel = ModelLoader.Load(modelAsset);

            // Create the Worker for inference using GPU Compute Shaders
            worker = new Worker(runtimeModel, BackendType.GPUCompute);
        }
        else
        {
            Debug.LogError("[IA]: ONNX Model not assigned in SpellAIReader!");
        }
    }

    /// Analyzes the extracted 64x64 texture and returns the identified class
    public string RecognizeSymbol(Texture2D symbolTexture, out float confidence)
    {
        confidence = 0f;
        if (worker == null) return "Garbage"; // Fallback

        // Convert the Texture2D into the TensorFloat required by Sentis
        // Define the shape: (1, 64, 64, 3) to (1, 224, 224, 3) for a single image with 3 color channels
        TensorShape inputShape = new TensorShape(1, 224, 224, 3);
        using Tensor<float> inputTensor = new Tensor<float>(inputShape);

        // Save for debug:
        // TextureSaver.SaveTextureAsPNG(symbolTexture, "AIReadSymbol_" + UnityEngine.Random.Range(0000, 9999)); // Save the texture for debugging
        
        // Explicitly tell the converter that your Tensor uses the NHWC layout,
        // and automatically normalize pixel bytes (0-255) down to the 0.0-1.0 range required by Teachable Machine.
        TextureTransform transform = new TextureTransform().SetTensorLayout(TensorLayout.NHWC);


        // This modifies your instantiated inputTensor in-place
        TextureConverter.ToTensor(symbolTexture, inputTensor, transform);

        // Execute calculations on the GPU
        worker.Schedule(inputTensor);

        // Retrieve output data from the network
        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;

        // Make the Tensor readable on the CPU side
        float[] predictions = outputTensor.DownloadToArray();

        // Find the index with the highest probability value
        int highestIndex = 0;
        float highestValue = float.MinValue;

        for (int i = 0; i < predictions.Length; i++)
        {
            if (predictions[i] > highestValue)
            {
                highestValue = predictions[i];
                highestIndex = i;
            }
        }

        // Clamp the highest value to a range of 0-1
        confidence = Mathf.Clamp01(highestValue);

        // Return the name of the corresponding class
        if (highestIndex < labels.Length)
        {
            return labels[highestIndex];
        }

        return "Garbage";
    }

    private void OnDestroy()
    {
        // Sentis uses unmanaged resources, dispose of them when the object is destroyed
        worker?.Dispose();
    }
}
