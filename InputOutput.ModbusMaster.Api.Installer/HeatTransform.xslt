<?xml version="1.0" encoding="utf-8"?>
<!--
  Injects ServiceInstall / ServiceControl into the harvested component that owns InputOutput.ModbusMaster.Api.exe.
  Service name must match WindowsServiceInstaller.ServiceName (FTTInputOutput).
-->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:wix="http://schemas.microsoft.com/wix/2006/wi"
    xmlns="http://schemas.microsoft.com/wix/2006/wi"
    exclude-result-prefixes="wix">

  <xsl:output method="xml" indent="yes" encoding="utf-8" />

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="wix:Component[wix:File[contains(@Source, 'InputOutput.ModbusMaster.Api.exe')]]">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
      <ServiceInstall
          Id="FttInputOutputServiceInstall"
          Name="$(var.WindowsServiceName)"
          DisplayName="$(var.ServiceDisplayName)"
          Description="FTT Input Output Modbus RTU API and UI"
          Type="ownProcess"
          Start="auto"
          Account="LocalSystem"
          ErrorControl="normal"
          Vital="yes"
          Interactive="no" />
      <ServiceControl
          Id="FttInputOutputServiceControl"
          Name="$(var.WindowsServiceName)"
          Start="install"
          Stop="both"
          Remove="uninstall"
          Wait="yes" />
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>
