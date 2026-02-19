// Types moved to FluxGuard core package (FluxGuard.Abstractions namespace)
// This file provides binary backward compatibility for FluxGuard.Remote consumers
using System.Runtime.CompilerServices;
using FluxGuard.Abstractions;

[assembly: TypeForwardedTo(typeof(IRemoteGuard))]
[assembly: TypeForwardedTo(typeof(RemoteGuardResult))]
