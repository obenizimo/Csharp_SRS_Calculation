# SRS_Calculation

# SRS Calculation Utility

This project is a C# application that calculates the Shock Response Spectrum (SRS) from acceleration data stored in a text file. The application reads acceleration data from a provided file, performs the SRS calculation, and displays both the calculated frequencies and corresponding peak accelerations. It also measures and reports the time taken to perform the SRS calculation in milliseconds.

## Features

- Reads acceleration data from a text file.
- Configurable sampling frequency.
- Performs SRS calculation using adjustable parameters such as:
  - Starting frequency
  - Damping ratio
  - Octave band parameter (1, 2, 3, or 4)
- Calculates and displays the frequency response and peak acceleration values.
- Measures and displays the runtime of the SRS calculation.

## Requirements

- .NET SDK (e.g., .NET 6.0 or later)


## Usage

When you run the application, you will be prompted to:

- Enter the name of the text file that contains the acceleration data.
- Enter the sampling frequency (Hz) of the data (e.g., 2000.0).

After providing the required information:

- The application reads the acceleration data from the file.
- It performs the SRS calculation (with default parameters: starting frequency = 100 Hz, damping ratio = 0.05, and octave band parameter = 3).
- It displays the frequency (Hz) and peak acceleration (G) values for each calculated band.
- The runtime of the SRS calculation (in milliseconds) will also be displayed.

## Project Structure

- **Program.cs**: Contains the main C# application code for performing the SRS calculation and handling I/O operations.
- **README.md**: Provides an overview of the project, features, requirements, and usage instructions.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---
Happy Computing!
