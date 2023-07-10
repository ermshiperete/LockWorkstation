using System.Net;
using LockWorkStationService;
using NUnit.Framework;

namespace LockWorkStationServiceTests;

public class UdpServerTests
{
	[SetUp]
	public void Setup()
	{
	}

	[TestCase("192.168.0.1", "192.168.0.1", ExpectedResult = true)]
	[TestCase("192.168.0.1", "192.168.0.2", ExpectedResult = false)]
	[TestCase("192.168.0.1", "192.168.0.1/32", ExpectedResult = true)]
	[TestCase("192.168.0.1", "192.168.0.0/24", ExpectedResult = true)]
	[TestCase("192.168.1.1", "192.168.0.0/24", ExpectedResult = false)]
	[TestCase("192.168.0.1", "192.168.0.0/23", ExpectedResult = true)]
	[TestCase("192.168.1.1", "192.168.0.0/23", ExpectedResult = true)]
	[TestCase("192.168.0.1", "192.168.1.0/23", ExpectedResult = true)]
	[TestCase("192.168.2.1", "192.168.0.0/23", ExpectedResult = false)]
	[TestCase("10.128.128.250", "10.128.0.0/9", ExpectedResult = true)]
	[TestCase("10.182.128.250", "10.128.0.0/9", ExpectedResult = true)]
	[TestCase("10.64.128.250", "10.128.0.0/9", ExpectedResult = false)]
	[TestCase("10.64.128.250", "10.0.0.0/8", ExpectedResult = true)]
	[TestCase("192.168.1.1", "10.0.0.0/8", ExpectedResult = false)]
	public bool IsInSubnet(string address, string subnet)
	{
		var sut = new UdpServer("0.0.0.0", 84758, subnet, null);
		var ipAddress = IPAddress.Parse(address);
		return UdpServer.IsInSubnet(ipAddress, subnet);
	}

	[TestCase("192.168.0.0/33", typeof(ArgumentException))]
	[TestCase("192.168.0.0/a", typeof(FormatException))]
	[TestCase("192.168.0.0/24/3", typeof(ArgumentException))]
	public void IsInSubnetInvalid(string subnet, Type expectedException)
	{
		var sut = new UdpServer("0.0.0.0", 84758, subnet, null);
		var ipAddress = IPAddress.Parse("192.168.0.1");
		Assert.That(() => UdpServer.IsInSubnet(ipAddress, subnet), Throws.InstanceOf(expectedException));
	}
}