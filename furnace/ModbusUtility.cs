public static class ModbusUtility
{
    public static float ToFloat(ushort[] registers)
    {
        byte[] bytes = new byte[4];
        bytes[1] = (byte)(registers[0] & 0xFF);
        bytes[0] = (byte)(registers[0] >> 8);
        bytes[3] = (byte)(registers[1] & 0xFF);
        bytes[2] = (byte)(registers[1] >> 8);
        return BitConverter.ToSingle(bytes, 0);
    }

    public static ushort[] FromFloat(float value)
    {
        int integerPart = (int)value;
        int tenthsDigit = (int)((value - integerPart) * 10) % 10;

        // Handle negative numbers
        if (tenthsDigit < 0)
            tenthsDigit = -tenthsDigit;

        return new ushort[]
        {
            (ushort)integerPart,
            (ushort)tenthsDigit
        };
    }
}
