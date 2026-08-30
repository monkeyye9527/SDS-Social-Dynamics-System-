#!/usr/bin/env python3
"""
ONI Mod Pack - 自动化构建脚本

用法:
    python build.py --all           # 构建所有模块
    python build.py --target core   # 仅构建核心
    python build.py --release       # 发布构建（优化 + 打包 ZIP）
    python build.py --dev           # 开发模式（快速构建）
    python build.py --watch         # 开发模式（监听文件变化）
    python build.py --install       # 构建后复制到游戏 Mods 目录

前置要求:
    - Python 3.8+
    - .NET SDK 6.0+ (用于 MSBuild)
    - 配置 gamepaths.yaml 指向 ONI 安装目录
"""

import argparse
import os
import shutil
import subprocess
import sys
import zipfile
from datetime import datetime
from pathlib import Path

PROJECT_ROOT = Path(__file__).parent.parent.parent.resolve()
SRC_DIR = PROJECT_ROOT / "src"
DIST_DIR = PROJECT_ROOT / "dist"
TOOLS_DIR = PROJECT_ROOT / "tools"
CONFIG_DIR = PROJECT_ROOT / "config"

OUTPUT_DIR = DIST_DIR / "ONIModPack"
ASSEMBLIES_DIR = OUTPUT_DIR / "assemblies"

GAME_PATHS = {}


def load_game_paths():
    global GAME_PATHS
    paths_file = CONFIG_DIR / "gamepaths.yaml"

    if not paths_file.exists():
        print(f"警告: 游戏路径配置文件不存在: {paths_file}")
        print("请复制 config/gamepaths.template.yaml 为 config/gamepaths.yaml 并填写正确路径")
        return False

    try:
        import yaml
        with open(paths_file, 'r', encoding='utf-8') as f:
            GAME_PATHS = yaml.safe_load(f)
        return True
    except ImportError:
        with open(paths_file, 'r', encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if ':' in line and not line.startswith('#'):
                    key, value = line.split(':', 1)
                    GAME_PATHS[key.strip()] = value.strip().strip('"').strip("'")
        return True
    except Exception as e:
        print(f"错误: 无法读取游戏路径配置: {e}")
        return False


def clean_output():
    print("清理输出目录...")
    if OUTPUT_DIR.exists():
        shutil.rmtree(OUTPUT_DIR)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)


def clean():
    print("清理构建目录...")
    shutil.rmtree("bin", ignore_errors=True)
    shutil.rmtree("obj", ignore_errors=True)
    shutil.rmtree("dist", ignore_errors=True)
    print("清理完成")


def build_project(target: str = "all", release: bool = False, dev_mode: bool = False):
    print(f"构建目标: {target}, 发布模式: {release}, 开发模式: {dev_mode}")

    configuration = "Release" if release else "Debug"
    
    msbuild_args = [
        "dotnet", "build",
        str(PROJECT_ROOT / "ONIModPack.csproj"),
        f"/p:Configuration={configuration}",
        "/nologo",
        "/verbosity:minimal"
    ]

    if dev_mode:
        msbuild_args.append("/p:DevBuild=true")

    print(f"执行 MSBuild...")

    try:
        result = subprocess.run(
            msbuild_args,
            capture_output=True,
            text=True,
            cwd=PROJECT_ROOT
        )

        if result.returncode != 0:
            print(f"编译错误:\n{result.stderr}")
            return False

        print(f"编译成功")
        return True

    except FileNotFoundError:
        print("错误: 未找到 dotnet 命令")
        print("请安装 .NET SDK")
        return False


def copy_assets():
    assets_src = PROJECT_ROOT / "assets"
    assets_dst = OUTPUT_DIR / "assets"

    if assets_src.exists():
        if assets_dst.exists():
            shutil.rmtree(assets_dst)
        shutil.copytree(assets_src, assets_dst)
        print(f"复制资源文件到 {assets_dst}")

    strings_src = PROJECT_ROOT / "assets" / "strings"
    strings_dst = OUTPUT_DIR / "strings"
    if strings_src.exists():
        if strings_dst.exists():
            shutil.rmtree(strings_dst)
        shutil.copytree(strings_src, strings_dst)


def copy_build_output():
    bin_dir = PROJECT_ROOT / "bin" / "Release" / "net48"
    
    if not bin_dir.exists():
        bin_dir = PROJECT_ROOT / "bin" / "Debug" / "net48"
    
    if not bin_dir.exists():
        bin_dir = PROJECT_ROOT / "bin" / "Release"
    
    if not bin_dir.exists():
        bin_dir = PROJECT_ROOT / "bin" / "Debug"
    
    exclude_dlls = {
        "Assembly-CSharp.dll",
        "Assembly-CSharp-firstpass.dll",
        "UnityEngine.dll",
        "UnityEngine.CoreModule.dll",
        "UnityEngine.AudioModule.dll",
        "UnityEngine.AnimationModule.dll",
        "UnityEngine.IMGUIModule.dll",
        "UnityEngine.InputLegacyModule.dll",
        "UnityEngine.PhysicsModule.dll",
        "UnityEngine.SpriteMaskModule.dll",
        "UnityEngine.TextRenderingModule.dll",
        "UnityEngine.UI.dll",
    }
    
    for file in bin_dir.glob("*.dll"):
        if file.name in exclude_dlls:
            continue
        shutil.copy(file, OUTPUT_DIR)
        print(f"复制 DLL: {file} -> {OUTPUT_DIR}")
    
    pdb_path = bin_dir / "ONIModPack.pdb"
    if pdb_path.exists():
        shutil.copy(pdb_path, OUTPUT_DIR)
        print(f"复制 PDB: {pdb_path} -> {OUTPUT_DIR}")
    
    mod_yaml_src = PROJECT_ROOT / "mod.yaml"
    mod_info_src = PROJECT_ROOT / "mod_info.yaml"
    
    if mod_yaml_src.exists():
        shutil.copy(mod_yaml_src, OUTPUT_DIR)
    if mod_info_src.exists():
        shutil.copy(mod_info_src, OUTPUT_DIR)
    
    config_src = PROJECT_ROOT / "config"
    config_dst = OUTPUT_DIR / "config"
    if config_src.exists():
        if config_dst.exists():
            shutil.rmtree(config_dst)
        shutil.copytree(config_src, config_dst)


def create_release_package():
    version = get_version()
    zip_name = f"ONIModPack-v{version}.zip"
    zip_path = DIST_DIR / zip_name

    print(f"创建发布包: {zip_name}")

    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zf:
        for file_path in OUTPUT_DIR.rglob("*"):
            if file_path.is_file():
                arcname = file_path.relative_to(DIST_DIR)
                zf.write(file_path, arcname)

    print(f"发布包已创建: {zip_path}")
    return zip_path


def get_version() -> str:
    mod_yaml = OUTPUT_DIR / "mod.yaml"
    if mod_yaml.exists():
        with open(mod_yaml, 'r', encoding='utf-8') as f:
            for line in f:
                if 'version:' in line:
                    return line.split(':')[1].strip().strip('"').strip("'")
    return "1.0.0"


def copy_to_game_mods():
    possible_paths = [
        Path.home() / "Documents/Klei/OxygenNotIncluded/mods/Local/ONIModPack",
        Path.home() / ".config/unity3d/Klei/Oxygen Not Included/mods/Local/ONIModPack",
        Path(GAME_PATHS.get("mods_folder", "")),
    ]

    for mods_path in possible_paths:
        if mods_path.parent.exists():
            if mods_path.exists():
                shutil.rmtree(mods_path)
            shutil.copytree(OUTPUT_DIR, mods_path)
            print(f"已复制到游戏 Mods 目录: {mods_path}")
            return True

    print("警告: 未能找到游戏 Mods 目录，请手动复制")
    return False


def watch_mode():
    try:
        from watchdog.observers import Observer
        from watchdog.events import FileSystemEventHandler
    except ImportError:
        print("错误: 请安装 watchdog: pip install watchdog")
        sys.exit(1)

    class BuildHandler(FileSystemEventHandler):
        def __init__(self):
            self.last_build = 0

        def on_modified(self, event):
            if event.src_path.endswith('.cs') or event.src_path.endswith('.yaml'):
                now = datetime.now().timestamp()
                if now - self.last_build > 2:
                    self.last_build = now
                    print(f"\n文件变更: {event.src_path}")
                    print("重新构建...")
                    main_build("all", False, True, False)

    print("开发模式已启动，监听源文件变化...")
    print("按 Ctrl+C 停止")

    event_handler = BuildHandler()
    observer = Observer()
    observer.schedule(event_handler, str(SRC_DIR), recursive=True)
    observer.schedule(event_handler, str(PROJECT_ROOT / "config"), recursive=True)
    observer.start()

    try:
        while True:
            import time
            time.sleep(1)
    except KeyboardInterrupt:
        observer.stop()

    observer.join()


def main_build(target: str, release: bool, dev_mode: bool, install: bool):
    print("=" * 50)
    print(f"ONI Mod Pack - {'发布' if release else '开发'}构建")
    print("=" * 50)

    load_game_paths()

    clean_output()

    success = build_project(target, release, dev_mode)

    if not success:
        print("构建失败!")
        return False

    copy_build_output()
    copy_assets()

    if release:
        create_release_package()

    if install:
        copy_to_game_mods()

    print("\n构建完成!")
    return True


def main():
    parser = argparse.ArgumentParser(description="ONI Mod Pack 构建工具")
    parser.add_argument(
        "--target",
        choices=["all", "core", "qol", "balance", "content"],
        default="all",
        help="构建目标 (默认: all)"
    )
    parser.add_argument(
        "--release",
        action="store_true",
        help="发布构建模式（优化 + 打包）"
    )
    parser.add_argument(
        "--dev",
        action="store_true",
        help="开发模式（快速构建，跳过部分检查）"
    )
    parser.add_argument(
        "--install",
        action="store_true",
        help="构建完成后复制到游戏 Mods 目录"
    )
    parser.add_argument(
        "--watch",
        action="store_true",
        help="开发模式：监听文件变化自动重建"
    )
    parser.add_argument(
        "--clean",
        action="store_true",
        help="清理构建目录（bin, obj, dist）"
    )

    args = parser.parse_args()

    if args.clean:
        clean()

    if args.watch:
        main_build(args.target, args.release, True, args.install)
        watch_mode()
    else:
        success = main_build(args.target, args.release, args.dev, args.install)
        sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()