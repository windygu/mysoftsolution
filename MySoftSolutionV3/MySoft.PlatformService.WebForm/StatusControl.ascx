﻿<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="StatusControl.ascx.cs"
    Inherits="MySoft.PlatformService.WebForm.StatusControl" %>
<%@ Import Namespace="System.Linq" %>
<% 
    int nodeIndex = 0;
    foreach (var node in nodelist)
    {
        var status = statuslist[node.Key];
%>
<div id='div<% =node.Key %>' style="<%= CurrentIndex == nodeIndex ?"": "display:none;" %>">
    <div>
        <b>服务器节点 =>
            <% =node.Key %>【<% = node.IP + ":" + node.Port %>】状态信息如下：</b>(<% =status.StartDate%>)
        <span style="margin-left: 100px;"><b>当前时间：</b><%=DateTime.Now %></span>
    </div>
    <hr />
    <div style="float: left; width: 30%; border: 3px dotted #ccc; margin: 5px; padding: 5px;">
        <ul>
            <li><b>汇总状态信息【最近<% =status.TotalHours %>小时】</b></li>
            <li>运行总时间：
                <%=(int)DateTime.Now.Subtract(status.StartDate).TotalSeconds%>
                秒</li>
            <li>请求数:
                <%=status.Summary.RequestCount%>
                次</li>
            <li>成功数:
                <%=status.Summary.SuccessCount%>
                次</li>
            <li>错误数:
                <%=status.Summary.ErrorCount%>
                次</li>
            <li>总耗时:
                <%=status.Summary.ElapsedTime%>
                毫秒</li>
            <li>请求总时间：
                <%=status.Summary.RunningSeconds%>
                秒</li>
            <li>每秒请求数:
                <%=status.Summary.AverageRequestCount%>
                次</li>
            <li>平均成功数:
                <%=status.Summary.AverageSuccessCount%>
                次</li>
            <li>平均错误数:
                <%=status.Summary.AverageErrorCount%>
                次</li>
            <li>平均耗时数:
                <%=status.Summary.AverageElapsedTime%>
                毫秒</li>
        </ul>
    </div>
    <div style="float: left; width: 30%; border: 3px dotted #ccc; margin: 5px; padding: 5px;">
        <ul>
            <li><b>当前状态信息</b></li>
            <li>时间:
                <%=status.Latest.CounterTime%></li>
            <li>请求数:
                <%=status.Latest.RequestCount%>
                次</li>
            <li>成功数:
                <%=status.Latest.SuccessCount%>
                次</li>
            <li>错误数:
                <%=status.Latest.ErrorCount%>
                次</li>
            <li>总耗时:
                <%=status.Latest.ElapsedTime%>
                毫秒</li>
            <li>平均耗时:
                <%=status.Latest.AverageElapsedTime%>
                毫秒</li>
        </ul>
    </div>
    <div style="float: left; width: 30%; border: 3px dotted #ccc; margin: 5px; padding: 5px;">
        <ul>
            <li><b>最高状态信息</b></li>
            <li>最大请求数:
                <%=status.Highest.RequestCount%>
                次 (<%= status.Highest.RequestCountCounterTime%>)</li>
            <li>最大成功数:
                <%=status.Highest.SuccessCount%>
                次 (<%= status.Highest.SuccessCountCounterTime%>)</li>
            <li>最大错误数:
                <%=status.Highest.ErrorCount%>
                次 (<%= status.Highest.ErrorCountCounterTime%>)</li>
            <li>最大耗时:
                <%=status.Highest.ElapsedTime%>
                毫秒 (<%= status.Highest.ElapsedTimeCounterTime%>)</li>
        </ul>
    </div>
    <div style="clear: both;">
    </div>
    <hr />
    <div>
        <ul>
            <%
        var clients = clientlist[node.Key];
            %>
            <li><b>客户端连接信息 =>
                <% =node.Key %>【<% = node.IP + ":" + node.Port %>】</b></li>
            <%
        int index = 0;
        var appclients = clients.GroupBy(p => p.AppName)
            .Select(p => new { AppName = p.Key, Clients = p.ToList() })
            .OrderBy(p => p.Clients.Count)
            .ToList();
        foreach (var app in appclients)
        {
            index++;
            %>
            <li style="width: 30%; float: left; border: 3px dotted #ccc; margin: 5px; padding: 5px;">
                <% = index%>=>【<%= app.AppName%>】<br />
                <% foreach (var client in app.Clients)
                   {
                %>
                <div>
                    <span title="<% = client.AppPath %>" style="cursor: pointer; <%= client.AppPath != null ? "": "color: red;" %>">
                        ■</span>
                    <%= client.IPAddress%>[<%=client.HostName%>](<font color="blue"><%= client.Count%></font>)
                </div>
                <% } %>
            </li>
            <% } %>
        </ul>
    </div>
    <div style="clear: both;">
    </div>
</div>
<% nodeIndex++;
    } %>