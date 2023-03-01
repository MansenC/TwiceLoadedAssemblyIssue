#include <Windows.h>

#include <iostream>
#include <filesystem>

#include <nethost.h>
#include <coreclr_delegates.h>
#include <hostfxr.h>

const char* ThisExecutableName = "NativeHost.exe";
const char* CSharpLibraryName = "Example.CSharpApplication.dll";

struct FunctionPointers
{
	hostfxr_initialize_for_dotnet_command_line_fn init;
	hostfxr_get_runtime_delegate_fn getDelegate;
	hostfxr_close_fn close;
	load_assembly_and_get_function_pointer_fn getFuncPtr;
};

bool loadHostfxr(FunctionPointers* pointers);
bool getLoadAssembly(std::filesystem::path applicationDirectory, FunctionPointers& pointers);

int main()
{
	// Get the path to our executable
	wchar_t modulePath[MAX_PATH];
	GetModuleFileNameW(NULL, modulePath, MAX_PATH);

	// Get the path to the directory of that executable, where all our other dlls are located
	std::filesystem::path applicationDirectory = std::filesystem::path(modulePath).parent_path();

	FunctionPointers functionPointers;
	if (!loadHostfxr(&functionPointers))
	{
		return -1;
	}

	if (!getLoadAssembly(applicationDirectory, functionPointers))
	{
		return -1;
	}

	std::wstring csharpLibPath = applicationDirectory / CSharpLibraryName;

	typedef void (CORECLR_DELEGATE_CALLTYPE* entrypoint)();
	entrypoint delegateFunction = nullptr;
	int rc = functionPointers.getFuncPtr(
		csharpLibPath.c_str(),
		L"Example.ExampleEntrypoint, Example.CSharpApplication",
		L"Entrypoint",
		L"Example.ExampleEntrypoint+EntrypointDelegate, Example.CSharpApplication",
		nullptr,
		reinterpret_cast<void**>(&delegateFunction));

	if (rc != 0)
	{
		std::cerr << "Could not get delegate function to ExampleEntrypoint. Error code 0x" << std::hex << rc;
		return -1;
	}

	delegateFunction();
	std::cout << "Execution returned to host process\n";
	return 0;
}

bool loadHostfxr(FunctionPointers* pointers)
{
	wchar_t hostfxrPath[MAX_PATH];
	size_t bufferSize = MAX_PATH;

	int returnCode = get_hostfxr_path(hostfxrPath, &bufferSize, nullptr);
	if (returnCode != 0)
	{
		std::cerr << "Could not get path to hostfxr: 0x" << std::hex << returnCode;
		return false;
	}

	HMODULE hostfxrLibrary = LoadLibraryW(hostfxrPath);
	if (hostfxrLibrary == nullptr)
	{
		std::cerr << "LoadLibraryW returned error code 0x" << std::hex << GetLastError();
		return false;
	}

	*pointers = FunctionPointers();
	pointers->init = (hostfxr_initialize_for_dotnet_command_line_fn)GetProcAddress(hostfxrLibrary, "hostfxr_initialize_for_dotnet_command_line");
	pointers->getDelegate = (hostfxr_get_runtime_delegate_fn)GetProcAddress(hostfxrLibrary, "hostfxr_get_runtime_delegate");
	pointers->close = (hostfxr_close_fn)GetProcAddress(hostfxrLibrary, "hostfxr_close");

	return pointers->init && pointers->getDelegate && pointers->close;
}

bool getLoadAssembly(std::filesystem::path applicationDirectory, FunctionPointers& pointers)
{
	std::wstring hostPath = applicationDirectory / ThisExecutableName;
	std::wstring applicationDirectoryString = applicationDirectory.wstring();

	hostfxr_initialize_parameters params = {};
	params.size = sizeof(params);
	params.host_path = hostPath.c_str();
	params.dotnet_root = applicationDirectoryString.c_str();

	std::wstring csharpLibPath = applicationDirectory / CSharpLibraryName;
	const char_t* args = csharpLibPath.c_str();

	hostfxr_handle handle;
	int rc = pointers.init(1, &args, &params, &handle);
	if (rc != 0 || handle == nullptr)
	{
		pointers.close(handle);
		std::cerr << "Could not initialize CoreCLR. Error code 0x" << std::hex << rc;
		return false;
	}

	void* delegate = nullptr;
	rc = pointers.getDelegate(
		handle,
		hdt_load_assembly_and_get_function_pointer,
		&delegate);

	pointers.close(handle);
	if (rc != 0 || delegate == nullptr)
	{
		std::cerr << "Could not get load_assembly_and_get_function_pointer delegate. Error code 0x" << std::hex << rc;
		return false;
	}

	pointers.getFuncPtr = (load_assembly_and_get_function_pointer_fn)delegate;
	return true;
}
