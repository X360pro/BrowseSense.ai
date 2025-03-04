#!/bin/bash

# Run the Node.js script
cd Tab\ Timer
echo "Running index.js that posts data from browser to DB ..."
node index.js

# Run the Python script
echo "Running topic modelling and grouping python script..."
python3 test_llama.py

cd ../
cd Dashboard
cd activity_dashboard
# Run the .NET project
echo "Running .NET project..."
dotnet run



