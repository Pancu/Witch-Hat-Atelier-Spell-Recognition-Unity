# Witch Hat Atelier Spell Symbol Recognizer - Unity Engine Sample Project

**Unity Engine** version, inspired by the web version: https://github.com/ytnrvdf/wha-spell-simulator

It recognizes Fire symbols and Columns, read below how to add more symbols 

**This project uses a neural network trained in https://teachablemachine.withgoogle.com/ to recognize if a specific shape is a specific symbol, and calculates the precision**

Check debug logs for spell recognition

## How does it work?

The system waits for the player input as a cluster of points, tries to calculate the center of the drawing using Bouding Boxes, and sets a specific radius.
If there are points inside the radius tolerance, and all the sectors of the circle are covered, then it recognizes the outer circle, and the spell is sent for analysis. (else it waits for at least another stroke)

Once in analysis, all of the spell content are subdivided (if 2 points are near each other => they are part of the same group) and sent to the ML model to evaluate

## How can I add more symbols? / How can I change the model?

You can add more symbols by:
- Training your own model in https://teachablemachine.withgoogle.com/ 
- Download it as a Tensorflow Savedmodel file
- Extract the folder and (if you're on **Windows**) run:
`python -m tf2onnx.convert --saved-model "[Downloaded Path]\converted_savedmodel\model.savedmodel" --output model.onnx`
(Make sure you have **tf2onnx installed**, if not run this before: `pip install tensorflow tf2onnx`)
- Replace the old model file with the created .onnx file in Assets/Models
- Make sure SpellAIReader references the new model
- Test it out!
