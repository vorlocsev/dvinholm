#include <windows.h>
#include <string>
#include <vector>
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    wchar_t module[32768] = {};
    const DWORD n = GetModuleFileNameW(nullptr, module, 32768);
    if (!n || n >= 32768) return 10;
    std::wstring base(module, n);
    base.erase(base.find_last_of(L"\\/") + 1);
    const std::wstring script = base + L"Binaries\\Updater-Banner.ps1";
    if (GetFileAttributesW(script.c_str()) == INVALID_FILE_ATTRIBUTES) {
        MessageBoxW(nullptr,L"Binaries\\Updater-Banner.ps1 is missing.",L"Dvinholm",MB_OK|MB_ICONERROR);
        return 11;
    }
    wchar_t win[MAX_PATH] = {};
    if (!GetSystemDirectoryW(win, MAX_PATH)) return 12;
    const std::wstring ps = std::wstring(win) + L"\\WindowsPowerShell\\v1.0\\powershell.exe";
    if (GetFileAttributesW(ps.c_str()) == INVALID_FILE_ATTRIBUTES) return 13;
#ifdef DVINHOLM_DIRECT_LAUNCH
    const wchar_t* mode = L"Launch";
#else
    const wchar_t* mode = L"UI";
#endif
    std::wstring command = L"\"" + ps +
        L"\" -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File \"" +
        script + L"\" -Mode " + mode;
    std::vector<wchar_t> args(command.begin(),command.end());
    args.push_back(0);
    STARTUPINFOW si = {};
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi = {};
    if (!CreateProcessW(ps.c_str(), args.data(), nullptr, nullptr, FALSE,
                        CREATE_NO_WINDOW, nullptr, base.c_str(), &si, &pi)) {
        MessageBoxW(nullptr,L"Unable to start the Dvinholm process.",
                    L"Dvinholm",MB_OK|MB_ICONERROR);
        return 14;
    }
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}
