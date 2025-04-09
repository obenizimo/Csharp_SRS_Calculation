using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;

public struct SRSResult
{
    public double[] Frequencies; // Calculated frequencies
    public double[] Peaks;       // Corresponding peak acceleration values
    public int Count;            // Number of elements
}

public class Program
{
    /// <summary>
    /// C# version of the srs_hesaplama function.
    /// ivmeData: array containing acceleration values
    /// dataCount: number of data elements in the array
    /// sr: sampling frequency (Hz)
    /// startingFrequency: starting frequency (Hz)
    /// dampingRatio: damping ratio
    /// ioct: octave band parameter (1, 2, 3, or 4)
    /// </summary>
    public static SRSResult SrsHesaplama(float[] ivmeData, int dataCount, double sr, double startingFrequency, double dampingRatio, int ioct)
    {
        if (dataCount <= 0)
        {
            Console.Error.WriteLine("Input acceleration data cannot be empty.");
            Environment.Exit(1);
        }
        if (sr <= 0)
        {
            Console.Error.WriteLine("Sampling frequency must be positive.");
            Environment.Exit(1);
        }
        if (startingFrequency <= 0)
        {
            Console.Error.WriteLine("Starting frequency must be positive.");
            Environment.Exit(1);
        }
        if (dampingRatio <= 0)
        {
            Console.Error.WriteLine($"Warning: Damping ratio ({dampingRatio:0.000000}) <= 0. A very small positive value (1e-9) will be used.");
            dampingRatio = 1e-9;
        }
        if (ioct < 1 || ioct > 4)
        {
            Console.Error.WriteLine("Invalid octave band parameter (ioct should be between 1 and 4).");
            Environment.Exit(1);
        }

        // Fixed parameters (algor and ire values are set as in the C code)

        int last = dataCount;
        double dt = 1.0 / sr;
        double nyquistFreq = sr / 2.0;
        double maxAnalysisFreq = sr / 8.0;

        // --- 1. Initialize Frequencies ---
        List<double> fn = new List<double>();
        fn.Add(startingFrequency);
        int fn_count = 1;

        double scc;
        if (ioct == 1)
            scc = 1.0 / 3.0;
        else if (ioct == 2)
            scc = 1.0 / 6.0;
        else if (ioct == 3)
            scc = 1.0 / 12.0;
        else
            scc = 1.0 / 24.0;

        int j = 1;
        while (true)
        {
            double next_fn = fn[0] * Math.Pow(2.0, j * scc);
            if (next_fn > maxAnalysisFreq)
                break;
            if (next_fn >= nyquistFreq)
            {
                Console.Error.WriteLine($"Warning: Calculated frequency ({next_fn:0.000000} Hz) is approaching or exceeds the Nyquist frequency ({nyquistFreq:0.000000} Hz). Analysis stopped at {fn[fn_count - 1]:0.000000} Hz.");
                break;
            }
            fn.Add(next_fn);
            fn_count++;
            j++;
        }
        if (fn_count <= 0)
        {
            Console.Error.WriteLine("No valid frequencies found for analysis.");
            Environment.Exit(1);
        }

        // Variables for storing responses
        double[] xmax = new double[fn_count];
        double[] xmin = new double[fn_count];
        double[] x = new double[fn_count];
        double[] xb = new double[fn_count];
        double[] xbb = new double[fn_count];

        for (int i = 0; i < fn_count; i++)
        {
            xmax[i] = -1.0e+90;
            xmin[i] = 1.0e+90;
            x[i] = 0.0;
            xb[i] = 0.0;
            xbb[i] = 0.0;
        }

        double yb_state = 0.0;
        double ybb_state = 0.0;

        // --- 2. Initialize Filter Coefficients ---
        double[] sm_a1 = new double[fn_count];
        double[] sm_a2 = new double[fn_count];
        double[] sm_b1 = new double[fn_count];
        double[] sm_b2 = new double[fn_count];
        double[] sm_b3 = new double[fn_count];

        for (int k = 0; k < fn_count; k++)
        {
            double omega = 2.0 * Math.PI * fn[k];
            double omega_d = omega * Math.Sqrt(1.0 - dampingRatio * dampingRatio);
            if (Math.Abs(omega) < 1e-10 || double.IsNaN(omega_d))
            {
                Console.Error.WriteLine($"Warning: Issue in computing coefficients for frequency {fn[k]:0.000000} Hz. Response will be set to 0.");
                sm_a1[k] = sm_a2[k] = sm_b1[k] = sm_b2[k] = sm_b3[k] = 0.0;
                continue;
            }
            double E = Math.Exp(-dampingRatio * omega * dt);
            double K = omega_d * dt;
            double C = E * Math.Cos(K);
            double S = E * Math.Sin(K);
            double Sp = (Math.Abs(K) < 1e-9) ? E : (S / K);
            sm_a1[k] = 2.0 * C;
            sm_a2[k] = -E * E;
            sm_b1[k] = 1.0 - Sp;
            sm_b2[k] = 2.0 * (Sp - C);
            sm_b3[k] = E * E - Sp;
        }

        // --- 3. Calculate SDOF Response ---
        for (int i = 0; i < last; i++)
        {
            double yy = ivmeData[i];
            for (int k = 0; k < fn_count; k++)
            {
                x[k] = sm_a1[k] * xb[k] + sm_a2[k] * xbb[k] +
                       sm_b1[k] * yy + sm_b2[k] * yb_state + sm_b3[k] * ybb_state;
                if (x[k] > xmax[k])
                    xmax[k] = x[k];
                if (x[k] < xmin[k])
                    xmin[k] = x[k];
                xbb[k] = xb[k];
                xb[k] = x[k];
            }
            ybb_state = yb_state;
            yb_state = yy;
        }

        // --- 4. Prepare Output ---
        double[] srs_peak_values = new double[fn_count];
        for (int k = 0; k < fn_count; k++)
        {
            if (xmax[k] < -1.0e+80 && xmin[k] > 1.0e+80)
                srs_peak_values[k] = 0.0;
            else if (xmax[k] < -1.0e+80)
                srs_peak_values[k] = Math.Abs(xmin[k]);
            else if (xmin[k] > 1.0e+80)
                srs_peak_values[k] = xmax[k];
            else
                srs_peak_values[k] = (xmax[k] > Math.Abs(xmin[k])) ? xmax[k] : Math.Abs(xmin[k]);
        }

        return new SRSResult
        {
            Frequencies = fn.ToArray(),
            Peaks = srs_peak_values,
            Count = fn_count
        };
    }

    public static void Main()
    {
        Console.Write("Please enter the name of the .txt file that contains the acceleration data: ");
        string filename = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(filename))
        {
            Console.Error.WriteLine("File name could not be read.");
            Environment.Exit(1);
        }

        Console.Write("Please enter the sampling frequency of the data (Hz) (e.g., 2000.0): ");
        string sampleRateInput = Console.ReadLine();
        if (!double.TryParse(sampleRateInput, NumberStyles.Float, CultureInfo.InvariantCulture, out double sampleRate))
        {
            Console.Error.WriteLine("Sampling frequency could not be read.");
            Environment.Exit(1);
        }
        if (sampleRate <= 0)
        {
            Console.Error.WriteLine("Error: Sampling frequency must be a positive value.");
            Environment.Exit(1);
        }

        if (!File.Exists(filename))
        {
            Console.Error.WriteLine($"Error: Could not open file -> {filename}");
            Environment.Exit(1);
        }

        List<float> accelerationDataList = new List<float>();
        int lineNumber = 0;
        Console.WriteLine($"Reading file: {filename}...");
        foreach (string line in File.ReadLines(filename))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (!float.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                Console.Error.WriteLine($"Warning: Line {lineNumber} is not a valid number, skipping: '{line}'");
                continue;
            }
            accelerationDataList.Add(value);
        }

        if (accelerationDataList.Count == 0)
        {
            Console.Error.WriteLine("Error: No valid acceleration data was read from the file, or the file is empty.");
            Environment.Exit(1);
        }
        Console.WriteLine($"{accelerationDataList.Count} data points were successfully read from the file.");

        float[] accelerationData = accelerationDataList.ToArray();

        // Start measuring execution time of the SRS calculation
        Stopwatch stopwatch = Stopwatch.StartNew();
        // Perform SRS calculation (starting frequency 100 Hz, damping ratio 0.05, ioct = 3)
        SRSResult srsResult = SrsHesaplama(accelerationData, accelerationData.Length, sampleRate, 100.0, 0.05, 3);
        stopwatch.Stop();
        long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

        Console.WriteLine($"\nSRS calculation completed in {elapsedMilliseconds} ms.\n");
        Console.WriteLine("SRS Calculation Results:");
        Console.WriteLine("Frequency (Hz)\tPeak Acceleration (G)");
        Console.WriteLine("-------------\t---------------------");
        for (int i = 0; i < srsResult.Count; i++)
        {
            Console.WriteLine($"{srsResult.Frequencies[i]:0.0000}\t\t{srsResult.Peaks[i]:0.0000}");
        }
    }
}